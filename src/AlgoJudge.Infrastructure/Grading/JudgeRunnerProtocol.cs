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
        if (!TryParseFrame(protocolBytes, 0, out result, out var consumed))
            return false;

        return consumed == protocolBytes.Length;
    }

    public static bool TryParseBatch(
        byte[] protocolBytes,
        int requestedCaseCount,
        out IReadOnlyList<SandboxRunResult> results)
    {
        results = [];
        if (requestedCaseCount <= 0)
            return false;

        var parsed = new List<SandboxRunResult>(requestedCaseCount);
        var offset = 0;
        while (offset < protocolBytes.Length && parsed.Count < requestedCaseCount)
        {
            if (!TryParseFrame(protocolBytes, offset, out var result, out var consumed))
                return false;

            parsed.Add(result);
            offset += consumed;
            if (result.Status != SandboxRunStatus.Success)
                break;
        }

        if (offset != protocolBytes.Length || parsed.Count == 0)
            return false;
        if (parsed.Count != requestedCaseCount &&
            parsed[^1].Status == SandboxRunStatus.Success)
        {
            return false;
        }

        results = parsed;
        return true;
    }

    private static bool TryParseFrame(
        byte[] protocolBytes,
        int offset,
        out SandboxRunResult result,
        out int consumed)
    {
        result = new SandboxRunResult { Status = SandboxRunStatus.SystemError };
        consumed = 0;
        var headerEnd = FindHeaderEnd(protocolBytes, offset);
        if (headerEnd < 0)
            return false;

        var header = Encoding.ASCII.GetString(
            protocolBytes,
            offset,
            headerEnd - offset);
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
        var frameEnd = (long)payloadStart + stdoutLength + stderrLength;
        if (frameEnd > protocolBytes.Length)
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
        consumed = checked((int)frameEnd - offset);
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

    private static int FindHeaderEnd(byte[] bytes, int offset)
    {
        var searchLimit = Math.Min(bytes.Length - 1, offset + OverheadBytes);
        for (var index = offset; index < searchLimit; index++)
        {
            if (bytes[index] == (byte)'\n' && bytes[index + 1] == (byte)'\n')
                return index;
        }

        return -1;
    }
}
