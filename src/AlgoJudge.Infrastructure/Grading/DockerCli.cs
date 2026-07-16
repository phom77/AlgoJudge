using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace AlgoJudge.Infrastructure.Grading;

internal sealed class DockerCli
{
    private const int ControlOutputLimitBytes = 16 * 1024;

    private readonly TimeSpan _controlTimeout;
    private readonly ILogger _logger;

    public DockerCli(TimeSpan controlTimeout, ILogger logger)
    {
        _controlTimeout = controlTimeout;
        _logger = logger;
    }

    public async Task CreateAsync(
        IReadOnlyCollection<string> arguments,
        CancellationToken cancellationToken)
    {
        var result = await RunAsync(
            arguments,
            stdin: null,
            timeout: _controlTimeout,
            stdoutLimitBytes: ControlOutputLimitBytes,
            stderrLimitBytes: ControlOutputLimitBytes,
            cancellationToken);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                "Docker could not create the judge container " +
                $"(exit code {result.ExitCode}, stderr bytes {result.Stderr.Bytes.Length}, " +
                $"truncated {result.Stderr.Truncated}).");
        }
    }

    public Task<DockerCommandResult> StartAsync(
        string containerName,
        string? stdin,
        TimeSpan timeout,
        int stdoutLimitBytes,
        int stderrLimitBytes,
        CancellationToken cancellationToken)
    {
        var arguments = new List<string> { "start", "--attach" };
        if (stdin is not null)
            arguments.Add("--interactive");
        arguments.Add(containerName);

        return RunAsync(
            arguments,
            stdin,
            timeout,
            stdoutLimitBytes,
            stderrLimitBytes,
            cancellationToken);
    }

    public async Task<DockerContainerState> InspectAsync(
        string containerName,
        CancellationToken cancellationToken)
    {
        var result = await RunAsync(
            [
                "inspect",
                "--format",
                "{{.State.Status}}|{{.State.ExitCode}}|{{.State.OOMKilled}}",
                containerName
            ],
            stdin: null,
            timeout: _controlTimeout,
            stdoutLimitBytes: ControlOutputLimitBytes,
            stderrLimitBytes: ControlOutputLimitBytes,
            cancellationToken);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                "Docker could not inspect the judge container " +
                $"(exit code {result.ExitCode}, stderr bytes {result.Stderr.Bytes.Length}, " +
                $"truncated {result.Stderr.Truncated}).");
        }

        var fields = DecodeUtf8(result.Stdout.Bytes).Trim().Split('|');
        if (fields.Length != 3 ||
            !int.TryParse(
                fields[1],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var exitCode) ||
            !bool.TryParse(fields[2], out var oomKilled))
        {
            throw new InvalidOperationException("Docker returned an invalid container state.");
        }

        return new DockerContainerState(fields[0], exitCode, oomKilled);
    }

    public async Task RemoveAsync(string containerName)
    {
        try
        {
            await RunAsync(
                ["rm", "--force", containerName],
                stdin: null,
                timeout: TimeSpan.FromSeconds(10),
                stdoutLimitBytes: ControlOutputLimitBytes,
                stderrLimitBytes: ControlOutputLimitBytes,
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Could not remove judge container {ContainerName}.",
                containerName);
        }
    }

    private static async Task<DockerCommandResult> RunAsync(
        IReadOnlyCollection<string> arguments,
        string? stdin,
        TimeSpan timeout,
        int stdoutLimitBytes,
        int stderrLimitBytes,
        CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "docker",
                RedirectStandardInput = stdin is not null,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        foreach (var argument in arguments)
            process.StartInfo.ArgumentList.Add(argument);

        if (!process.Start())
            throw new InvalidOperationException("Docker CLI could not be started.");

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        timeoutSource.CancelAfter(timeout);

        var stdoutTask = ReadBoundedAsync(
            process.StandardOutput.BaseStream,
            stdoutLimitBytes,
            timeoutSource.Token);
        var stderrTask = ReadBoundedAsync(
            process.StandardError.BaseStream,
            stderrLimitBytes,
            timeoutSource.Token);
        var stdinTask = stdin is null
            ? Task.CompletedTask
            : WriteStandardInputAsync(process, stdin, timeoutSource.Token);

        try
        {
            await process.WaitForExitAsync(timeoutSource.Token);
            try
            {
                await stdinTask;
            }
            catch (IOException)
            {
                // The judged process may exit before consuming all test input.
            }

            return new DockerCommandResult(
                process.ExitCode,
                await stdoutTask,
                await stderrTask);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryKillProcess(process);
            throw new TimeoutException(
                $"Docker command exceeded its {timeout.TotalSeconds:F0}-second watchdog.");
        }
        catch
        {
            TryKillProcess(process);
            throw;
        }
    }

    private static async Task WriteStandardInputAsync(
        Process process,
        string input,
        CancellationToken cancellationToken)
    {
        await process.StandardInput.WriteAsync(input.AsMemory(), cancellationToken);
        await process.StandardInput.FlushAsync(cancellationToken);
        process.StandardInput.Close();
    }

    private static async Task<BoundedBytes> ReadBoundedAsync(
        Stream stream,
        int limitBytes,
        CancellationToken cancellationToken)
    {
        var rentedBuffer = ArrayPool<byte>.Shared.Rent(8192);
        try
        {
            using var captured = new MemoryStream(capacity: Math.Min(limitBytes, 64 * 1024));
            var truncated = false;

            while (true)
            {
                var read = await stream.ReadAsync(rentedBuffer, cancellationToken);
                if (read == 0)
                    break;

                var remaining = limitBytes - (int)captured.Length;
                var copyCount = Math.Min(read, Math.Max(remaining, 0));
                if (copyCount > 0)
                    captured.Write(rentedBuffer, 0, copyCount);
                if (copyCount < read)
                    truncated = true;
            }

            return new BoundedBytes(captured.ToArray(), truncated);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rentedBuffer);
        }
    }

    private static void TryKillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Container cleanup is handled separately by name.
        }
    }

    private static string DecodeUtf8(byte[] bytes) => Encoding.UTF8.GetString(bytes);
}

internal sealed record BoundedBytes(byte[] Bytes, bool Truncated);

internal sealed record DockerCommandResult(
    int ExitCode,
    BoundedBytes Stdout,
    BoundedBytes Stderr);

internal sealed record DockerContainerState(
    string Status,
    int ExitCode,
    bool OomKilled);
