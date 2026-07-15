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

            // Build the docker run command used for compilation.
            //
            //  --rm                      removes the container after it exits
            //  --network none            disables network access
            //  --memory 512m             bounds compiler memory
            //  --cpus 1                  limits CPU allocation
            //  --read-only               makes the container filesystem read-only
            //  --tmpfs /tmp              allows g++ to write temporary files
            //  -v workDir:/sandbox       mounts the isolated host work directory
            //  --security-opt no-new-privileges  prevents privilege escalation
            //
            // The compiler must write the solution binary to /sandbox. The
            // read-only flag applies to the image filesystem, while the mounted
            // work directory remains writable for compilation artifacts.

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
            // Convert the configured KiB limit to Docker's MiB flag and allow
            // 64 MiB of runtime overhead for the stack and system libraries.
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

            // The outer watchdog includes a 10-second infrastructure buffer.
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
        /// Runs `docker` with the supplied arguments.
        /// Returns (exitCode, stdout, stderr).
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

            // Write stdin and close it immediately so the process cannot wait
            // indefinitely for additional input.
            if (!string.IsNullOrEmpty(stdin))
                await process.StandardInput.WriteAsync(stdin);
            process.StandardInput.Close();

            // Read stdout and stderr concurrently to avoid full-buffer deadlocks.
            var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
            var stderrTask = process.StandardError.ReadToEndAsync(ct);

            var finished = process.WaitForExit(timeoutMs);

            if (!finished)
            {
                // The outer Docker process timed out despite the inner timeout.
                _logger.LogWarning("Docker outer timeout reached ({Ms}ms). Killing docker process.", timeoutMs);
                try { process.Kill(entireProcessTree: true); } catch { }
            }

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            return (finished ? process.ExitCode : 124, stdout, stderr);
        }

        /// <summary>
        /// Reads peak memory from the completed container's cgroup.
        /// This works only on Linux; Windows and WSL2 return 0.
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
                // Wait for Docker to populate the cid file after container start.
                // Retry up to ten times at 100 ms intervals.
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

                // Try cgroup v2 first (Ubuntu 22.04+).
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

                // Fall back to cgroup v1 (Ubuntu 20.04 and earlier).
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
