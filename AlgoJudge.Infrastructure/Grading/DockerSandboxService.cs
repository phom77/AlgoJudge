using AlgoJudge.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace AlgoJudge.Infrastructure.Grading
{
    public class DockerSandboxService : IDockerSandbox
    {
        private readonly ILogger<DockerSandboxService> _logger;
        private readonly string _dockerImage;

        private const string ContainerWorkDir = "/sandbox";
        private const string SourceFileName = "solution.cpp";
        private const string BinaryFileName = "solution";

        public DockerSandboxService(
            IConfiguration configuration,
            ILogger<DockerSandboxService> logger)
        {
            _logger = logger;
            _dockerImage = configuration["Sandbox:DockerImage"] ?? "gcc:14";
        }

        public async Task<SandboxCompileResult> CompileAsync(string sourceCode, string workDir, CancellationToken ct = default)
        {
            var sourceFile = Path.Combine(workDir, SourceFileName);
            await File.WriteAllTextAsync(
                Path.Combine(workDir, SourceFileName),
                sourceCode,
                new UTF8Encoding(false), 
                ct);

            // Dựng lệnh docker run để compile
            //
            //  --rm                      tự xóa container sau khi chạy xong
            //  --network none            không có internet
            //  --memory 512m             compile không cần nhiều RAM
            //  --cpus 1                  giới hạn CPU
            //  --read-only               filesystem container read-only...
            //  --tmpfs /tmp              ...ngoại trừ /tmp (g++ cần ghi temp file)
            //  --tmpfs /sandbox          ...và /sandbox (nơi ta mount workDir vào)
            //  -v workDir:/sandbox       mount thư mục host vào container
            //  --security-opt no-new-privileges  không leo thang đặc quyền
            //
            // Lý do dùng --tmpfs /sandbox thay vì -v trực tiếp vào read-only:
            // Ta cần g++ ghi file output (solution binary) vào /sandbox,
            // nhưng --read-only chặn toàn bộ write. Giải pháp: mount workDir
            // bình thường (không read-only), chỉ flag --read-only ảnh hưởng
            // filesystem của image, không ảnh hưởng volume mount.

            var compileCmd =
                $"g++ /sandbox/{SourceFileName} " +
                $"-o /sandbox/{BinaryFileName} " +
                $"-O2 -std=c++17 -lm";

            var args = BuildDockerArgs(
                memoryFlag: "512m",
                needStdin: false,
                cidFile: null,
                command: compileCmd,
                workDir: workDir);

            _logger.LogDebug("Compile docker args: {Args}", args);

            var (exitCode, _, stderr) = await RunDockerProcessAsync(
                args, stdin: null, timeoutMs: 30_000, ct);

            if (exitCode != 0)
            {
                _logger.LogInformation("Compile failed. stderr: {Err}", stderr);
                return new SandboxCompileResult { Success = false, ErrorOutput = stderr };
            }

            return new SandboxCompileResult { Success = true };
        }

        public async Task<SandboxRunResult> RunAsync(string workDir, string input, int timeLimitMs, int memoryLimitKb, CancellationToken ct = default)
        {
            // Chuyển KB → bytes để truyền cho Docker (Docker nhận đơn vị m/g/k)
            // Cộng thêm 64MB overhead cho runtime (stack, libc, v.v.)
            var memoryMb = (memoryLimitKb / 1024) + 64;
            var memoryFlag = $"{memoryMb}m";

            var timeLimitSeconds = Math.Ceiling(timeLimitMs / 1000.0);

            var runCmd = $"timeout --kill-after=2s {timeLimitSeconds}s /sandbox/{BinaryFileName}";

            var cidFile = Path.Combine(workDir, "container.cid");

            var args = BuildDockerArgs(
                memoryFlag: memoryFlag,
                needStdin: true,
                cidFile: cidFile,
                command: runCmd,
                workDir: workDir);

            _logger.LogDebug("Run docker args: {Args}", args);

            var stopwatch = Stopwatch.StartNew();

            // Timeout từ bên ngoài = timeLimitMs + 10 giây buffer
            var outerTimeoutMs = timeLimitMs + 10_000;

            var (exitCode, stdout, stderr) = await RunDockerProcessAsync(
                args, stdin: input, timeoutMs: outerTimeoutMs, ct);

            stopwatch.Stop();
            var elapsedMs = (int)stopwatch.ElapsedMilliseconds;
            var peakMemoryBytes = await ReadCgroupPeakMemoryAsync(cidFile);

            TryDeleteFile(cidFile);

            if (exitCode == 124)
            {
                _logger.LogInformation("TimeLimitExceeded — exit 124");
                return new SandboxRunResult
                {
                    Status = SandboxRunStatus.TimeLimitExceeded,
                    ExecutionTimeMs = timeLimitMs,
                    MemoryUsedBytes = 0,
                    Output = string.Empty
                };
            }

            if (exitCode != 0)
            {
                _logger.LogInformation("RuntimeError — exit code: {Code}, stderr: {Err}", exitCode, stderr);
                return new SandboxRunResult
                {
                    Status = SandboxRunStatus.RuntimeError,
                    ExecutionTimeMs = elapsedMs,
                    MemoryUsedBytes = 0,
                    Output = string.Empty
                };
            }

            return new SandboxRunResult
            {
                Status = SandboxRunStatus.Success,
                ExecutionTimeMs = elapsedMs,
                MemoryUsedBytes = 0, 
                Output = stdout
            };
        }

        /// <summary>
        /// Build arguments for the command `docker run`.
        /// </summary>
        private string BuildDockerArgs(
            string memoryFlag,
            bool needStdin,
            string? cidFile,
            string command,
            string workDir)
        {
            var parts = new List<string>
            {
                "run",
                "--rm",
            };

            if (cidFile != null)
                parts.Add($"--cidfile \"{cidFile}\"");

            if (needStdin)
                parts.Add("-i");

            parts.AddRange(new[]
            {
                "--network none",
                $"--memory {memoryFlag}",
                "--memory-swap -1",
                "--cpus 1",
                "--read-only",
                "--tmpfs /tmp:size=64m",
                $"-v \"{ToDockerPath(workDir)}\":{ContainerWorkDir}",
                "--security-opt no-new-privileges",
                _dockerImage,
                "sh", "-c",
                $"\"{command}\""
            });

            return string.Join(" ", parts);
        }

        /// <summary>
        /// Chạy lệnh `docker` với arguments cho trước.
        /// Trả về (exitCode, stdout, stderr).
        /// </summary>
        private async Task<(int ExitCode, string Stdout, string Stderr)> RunDockerProcessAsync(
            string args,
            string? stdin,
            int timeoutMs,
            CancellationToken ct)
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = args,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            process.Start();

            // Ghi stdin rồi đóng ngay — tránh deadlock khi process chờ input mà ta không ghi thêm
            if (!string.IsNullOrEmpty(stdin))
                await process.StandardInput.WriteAsync(stdin);
            process.StandardInput.Close();

            // Đọc stdout và stderr song song — tránh deadlock khi buffer đầy
            var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
            var stderrTask = process.StandardError.ReadToEndAsync(ct);

            var finished = process.WaitForExit(timeoutMs);

            if (!finished)
            {
                // Docker process bên ngoài bị timeout (không nên xảy ra nếu timeout bên trong hoạt động đúng)
                _logger.LogWarning("Docker outer timeout reached ({Ms}ms). Killing docker process.", timeoutMs);
                try { process.Kill(entireProcessTree: true); } catch { }
            }

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            return (finished ? process.ExitCode : 124, stdout, stderr);
        }

        /// <summary>
        /// Đọc peak memory từ cgroup của container vừa chạy xong.
        /// Chỉ hoạt động trên Linux. Windows/WSL2 trả về 0.
        /// </summary>
        private async Task<long> ReadCgroupPeakMemoryAsync(string cidFile)
        {
            if (!OperatingSystem.IsLinux())
            {
                _logger.LogDebug("Non-Linux OS — memory measurement not available.");
                return 0;
            }

            try
            {
                // Đợi Docker ghi xong cidFile (container đã start)
                // Thử tối đa 10 lần, mỗi lần cách nhau 100ms
                string? containerId = null;
                for (var i = 0; i < 10; i++)
                {
                    if (File.Exists(cidFile))
                    {
                        containerId = (await File.ReadAllTextAsync(cidFile)).Trim();
                        if (!string.IsNullOrEmpty(containerId)) break;
                    }
                    await Task.Delay(100);
                }

                if (string.IsNullOrEmpty(containerId))
                {
                    _logger.LogWarning("cidFile not found or empty: {Path}", cidFile);
                    return 0;
                }

                // Thử cgroup v2 trước (Ubuntu 22.04+)
                // cgroup v2: /sys/fs/cgroup/system.slice/docker-{id}.scope/memory.peak
                var cgroupV2Path = $"/sys/fs/cgroup/system.slice/docker-{containerId}.scope/memory.peak";
                if (File.Exists(cgroupV2Path))
                {
                    var raw = await File.ReadAllTextAsync(cgroupV2Path);
                    if (long.TryParse(raw.Trim(), out var bytesV2))
                    {
                        _logger.LogDebug("cgroup v2 peak memory: {Bytes} bytes.", bytesV2);
                        return bytesV2;
                    }
                }

                // Fallback cgroup v1 (Ubuntu 20.04 trở xuống)
                // cgroup v1: /sys/fs/cgroup/memory/docker/{id}/memory.max_usage_in_bytes
                var cgroupV1Path = $"/sys/fs/cgroup/memory/docker/{containerId}/memory.max_usage_in_bytes";
                if (File.Exists(cgroupV1Path))
                {
                    var raw = await File.ReadAllTextAsync(cgroupV1Path);
                    if (long.TryParse(raw.Trim(), out var bytesV1))
                    {
                        _logger.LogDebug("cgroup v1 peak memory: {Bytes} bytes.", bytesV1);
                        return bytesV1;
                    }
                }

                _logger.LogWarning("cgroup path not found for container {Id}.", containerId);
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read cgroup memory.");
                return 0;
            }
        }

        private static void TryDeleteFile(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        private static string ToDockerPath(string path)
        {
            if (!OperatingSystem.IsWindows()) return path;

            // C:\foo\bar → /c/foo/bar
            return "/" + path[0].ToString().ToLower() + path[2..].Replace('\\', '/');
        }
    }
}
