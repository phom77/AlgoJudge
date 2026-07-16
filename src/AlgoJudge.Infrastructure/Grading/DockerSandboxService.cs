using AlgoJudge.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text;

namespace AlgoJudge.Infrastructure.Grading;

public sealed class DockerSandboxService : IDockerSandbox
{
    private const string ContainerUser = "10001:10001";
    private const string CompileWorkDirectory = "/workspace";
    private const string RuntimeWorkDirectory = "/artifact";
    private const string SourceFileName = "solution.cpp";
    private const string BinaryFileName = "solution";
    private const int CompileMemoryMb = 512;

    private readonly ILogger<DockerSandboxService> _logger;
    private readonly DockerSandboxOptions _options;
    private readonly DockerCli _docker;

    public DockerSandboxService(
        IConfiguration configuration,
        ILogger<DockerSandboxService> logger)
    {
        _logger = logger;
        _options = DockerSandboxOptions.FromConfiguration(configuration);
        _docker = new DockerCli(_options.DockerStartupAllowance, logger);
    }

    public async Task<SandboxCompileResult> CompileAsync(
        string sourceCode,
        string workDir,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(sourceCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(workDir);

        var fullWorkDirectory = Path.GetFullPath(workDir);
        Directory.CreateDirectory(fullWorkDirectory);
        PrepareCompileDirectory(fullWorkDirectory);

        var sourceFile = Path.Combine(fullWorkDirectory, SourceFileName);
        var binaryFile = Path.Combine(fullWorkDirectory, BinaryFileName);
        TryDeleteFile(sourceFile);
        TryDeleteFile(binaryFile);

        await File.WriteAllTextAsync(
            sourceFile,
            sourceCode,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            ct);
        MakeSourceReadable(sourceFile);

        var containerName = CreateContainerName("compile");
        try
        {
            var memory = $"{CompileMemoryMb}m";
            var createArguments = BuildBaseCreateArguments(containerName, memory);
            createArguments.AddRange([
                "--tmpfs", "/tmp:rw,nosuid,nodev,noexec,size=64m",
                "--volume", $"{ToDockerPath(fullWorkDirectory)}:{CompileWorkDirectory}:rw",
                "--workdir", CompileWorkDirectory,
                _options.Image,
                "g++",
                $"{CompileWorkDirectory}/{SourceFileName}",
                "-o", $"{CompileWorkDirectory}/{BinaryFileName}",
                "-O2",
                "-std=c++17",
                "-pipe",
                "-lm"
            ]);

            await _docker.CreateAsync(createArguments, ct);
            var startResult = await _docker.StartAsync(
                containerName,
                stdin: null,
                _options.CompileTimeout,
                _options.StdoutLimitBytes,
                _options.StderrLimitBytes,
                ct);
            var state = await _docker.InspectAsync(containerName, ct);

            if (!state.Status.Equals("exited", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Compiler container did not reach an exited state.");

            if (state.ExitCode != 0)
            {
                var diagnostics = DecodeUtf8(startResult.Stderr.Bytes);
                if (startResult.Stderr.Truncated)
                    diagnostics += "\n[compiler diagnostics truncated]";

                return new SandboxCompileResult
                {
                    Success = false,
                    ErrorOutput = diagnostics
                };
            }

            if (!File.Exists(binaryFile))
            {
                throw new InvalidOperationException(
                    "Compiler container exited successfully without producing an artifact.");
            }

            return new SandboxCompileResult { Success = true };
        }
        finally
        {
            await _docker.RemoveAsync(containerName);
        }
    }

    public async Task<SandboxRunResult> RunAsync(
        string workDir,
        string input,
        int timeLimitMs,
        int memoryLimitKb,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workDir);
        ArgumentNullException.ThrowIfNull(input);
        if (timeLimitMs <= 0)
            throw new ArgumentOutOfRangeException(nameof(timeLimitMs));
        if (memoryLimitKb < 16 * 1024)
        {
            throw new ArgumentOutOfRangeException(
                nameof(memoryLimitKb),
                "The Docker judge requires a memory limit of at least 16384 KiB.");
        }

        var fullWorkDirectory = Path.GetFullPath(workDir);
        var binaryFile = Path.Combine(fullWorkDirectory, BinaryFileName);
        if (!File.Exists(binaryFile))
            throw new FileNotFoundException("Compiled solution artifact was not found.", binaryFile);

        var containerName = CreateContainerName("run");
        try
        {
            var memory = $"{memoryLimitKb}k";
            var createArguments = BuildBaseCreateArguments(containerName, memory);
            createArguments.AddRange([
                "--interactive",
                "--volume", $"{ToDockerPath(binaryFile)}:{RuntimeWorkDirectory}/{BinaryFileName}:ro",
                "--workdir", RuntimeWorkDirectory,
                _options.Image,
                "/usr/local/bin/algojudge-runner",
                "--time-limit-ms", timeLimitMs.ToString(CultureInfo.InvariantCulture),
                "--stdout-limit-bytes", _options.StdoutLimitBytes.ToString(CultureInfo.InvariantCulture),
                "--stderr-limit-bytes", _options.StderrLimitBytes.ToString(CultureInfo.InvariantCulture),
                "--",
                $"{RuntimeWorkDirectory}/{BinaryFileName}"
            ]);

            await _docker.CreateAsync(createArguments, ct);

            DockerCommandResult startResult;
            try
            {
                startResult = await _docker.StartAsync(
                    containerName,
                    input,
                    TimeSpan.FromMilliseconds(timeLimitMs) + _options.DockerStartupAllowance,
                    checked(
                        _options.StdoutLimitBytes +
                        _options.StderrLimitBytes +
                        JudgeRunnerProtocol.OverheadBytes),
                    JudgeRunnerProtocol.OverheadBytes,
                    ct);
            }
            catch (TimeoutException exception)
            {
                _logger.LogError(
                    exception,
                    "The outer Docker watchdog expired for judge container {ContainerName}.",
                    containerName);
                return new SandboxRunResult { Status = SandboxRunStatus.SystemError };
            }

            var state = await _docker.InspectAsync(containerName, ct);
            if (state.OomKilled)
            {
                return new SandboxRunResult
                {
                    Status = SandboxRunStatus.MemoryLimitExceeded,
                    MemoryUsedBytes = (long)memoryLimitKb * 1024
                };
            }

            if (!state.Status.Equals("exited", StringComparison.OrdinalIgnoreCase) ||
                state.ExitCode != 0 ||
                startResult.Stdout.Truncated)
            {
                _logger.LogError(
                    "Judge runner failed. Container status {Status}, exit {ExitCode}, " +
                    "stdout truncated {StdoutTruncated}. Docker stderr captured " +
                    "{DockerStderrBytes} bytes, truncated {DockerStderrTruncated}.",
                    state.Status,
                    state.ExitCode,
                    startResult.Stdout.Truncated,
                    startResult.Stderr.Bytes.Length,
                    startResult.Stderr.Truncated);
                return new SandboxRunResult { Status = SandboxRunStatus.SystemError };
            }

            if (!JudgeRunnerProtocol.TryParse(startResult.Stdout.Bytes, out var result))
            {
                _logger.LogError(
                    "Judge runner returned an invalid protocol response. Docker stderr " +
                    "captured {DockerStderrBytes} bytes, truncated {DockerStderrTruncated}.",
                    startResult.Stderr.Bytes.Length,
                    startResult.Stderr.Truncated);
                return new SandboxRunResult { Status = SandboxRunStatus.SystemError };
            }

            return result;
        }
        finally
        {
            await _docker.RemoveAsync(containerName);
        }
    }

    private List<string> BuildBaseCreateArguments(string containerName, string memory)
    {
        return [
            "create",
            "--name", containerName,
            "--network", "none",
            "--memory", memory,
            "--memory-swap", memory,
            "--cpus", "1",
            "--pids-limit", _options.PidsLimit.ToString(CultureInfo.InvariantCulture),
            "--cap-drop", "ALL",
            "--security-opt", "no-new-privileges=true",
            "--read-only",
            "--user", ContainerUser,
            "--ulimit", "core=0:0",
            "--ulimit", "nofile=64:64"
        ];
    }

    private static string CreateContainerName(string stage)
    {
        return $"algojudge-{stage}-{Guid.NewGuid():N}";
    }

    private static string DecodeUtf8(byte[] bytes) => Encoding.UTF8.GetString(bytes);

    private static void PrepareCompileDirectory(string path)
    {
        if (!OperatingSystem.IsLinux())
            return;

        File.SetUnixFileMode(
            path,
            UnixFileMode.UserRead |
            UnixFileMode.UserWrite |
            UnixFileMode.UserExecute |
            UnixFileMode.GroupRead |
            UnixFileMode.GroupWrite |
            UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead |
            UnixFileMode.OtherWrite |
            UnixFileMode.OtherExecute);
    }

    private static void MakeSourceReadable(string path)
    {
        if (!OperatingSystem.IsLinux())
            return;

        File.SetUnixFileMode(
            path,
            UnixFileMode.UserRead |
            UnixFileMode.UserWrite |
            UnixFileMode.GroupRead |
            UnixFileMode.OtherRead);
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // A clear write/compile failure follows if a stale artifact cannot be removed.
        }
    }

    private static string ToDockerPath(string path)
    {
        if (!OperatingSystem.IsWindows())
            return path;

        return "/" + char.ToLowerInvariant(path[0]) + path[2..].Replace('\\', '/');
    }
}
