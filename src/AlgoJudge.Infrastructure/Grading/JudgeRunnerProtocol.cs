using AlgoJudge.Application.Interfaces;
using System.Globalization;
using System.Text;

namespace AlgoJudge.Infrastructure.Grading;

internal static class JudgeRunnerProtocol
{
    private const string Name = "ALGOJUDGE_RESULT_V1";

    public const int OverheadBytes = 4 * 1024;

    public static bool TryParse(byte[] protocolBytes, out SandboxRunResult result)
    {
        result = new SandboxRunResult { Status = SandboxRunStatus.SystemError };
        var headerEnd = FindHeaderEnd(protocolBytes);
        if (headerEnd < 0)
            return false;

        var header = Encoding.ASCII.GetString(protocolBytes, 0, headerEnd);
        var lines = header.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 6 || !lines[0].Equals(Name, StringComparison.Ordinal))
            return false;

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in lines.Skip(1))
        {
            var separator = line.IndexOf('=');
            if (separator <= 0)
                return false;
            values[line[..separator]] = line[(separator + 1)..];
        }

        if (!values.TryGetValue("status", out var statusValue) ||
            !TryReadNonNegativeLong(values, "elapsed_us", out var elapsedUs) ||
            !TryReadNonNegativeLong(values, "memory_bytes", out var memoryBytes) ||
            !TryReadLength(values, "stdout_length", out var stdoutLength) ||
            !TryReadLength(values, "stderr_length", out var stderrLength))
        {
            return false;
        }

        var payloadStart = headerEnd + 2;
        if ((long)payloadStart + stdoutLength + stderrLength != protocolBytes.Length)
            return false;

        var sandboxStatus = statusValue switch
        {
            "success" => SandboxRunStatus.Success,
            "time_limit_exceeded" => SandboxRunStatus.TimeLimitExceeded,
            "memory_limit_exceeded" => SandboxRunStatus.MemoryLimitExceeded,
            "output_limit_exceeded" => SandboxRunStatus.OutputLimitExceeded,
            "runtime_error" => SandboxRunStatus.RuntimeError,
            _ => SandboxRunStatus.SystemError
        };
        if (sandboxStatus == SandboxRunStatus.SystemError)
            return false;

        result = new SandboxRunResult
        {
            Status = sandboxStatus,
            Output = Encoding.UTF8.GetString(protocolBytes, payloadStart, stdoutLength),
            ErrorOutput = Encoding.UTF8.GetString(
                protocolBytes,
                payloadStart + stdoutLength,
                stderrLength),
            ExecutionTimeMs = (int)Math.Min(int.MaxValue, (elapsedUs + 999) / 1000),
            MemoryUsedBytes = memoryBytes
        };
        return true;
    }

    private static bool TryReadNonNegativeLong(
        IReadOnlyDictionary<string, string> values,
        string key,
        out long value)
    {
        value = 0;
        return values.TryGetValue(key, out var text) &&
            long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) &&
            value >= 0;
    }

    private static bool TryReadLength(
        IReadOnlyDictionary<string, string> values,
        string key,
        out int value)
    {
        value = 0;
        return values.TryGetValue(key, out var text) &&
            int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) &&
            value >= 0;
    }

    private static int FindHeaderEnd(byte[] bytes)
    {
        var searchLimit = Math.Min(bytes.Length - 1, OverheadBytes);
        for (var index = 0; index < searchLimit; index++)
        {
            if (bytes[index] == (byte)'\n' && bytes[index + 1] == (byte)'\n')
                return index;
        }

        return -1;
    }
}
