using System.Text;
using System.Text.RegularExpressions;
using AlgoJudge.Application.Contracts.Submissions;

namespace AlgoJudge.Infrastructure.Grading;

public static partial class CompilerDiagnosticSanitizer
{
    private const string GeneratedHarnessFileName = "algojudge-harness.cpp";
    private const string SubmissionFileName = "submission.cpp";
    private const string GenericFunctionMessage =
        "Compilation failed. Verify that the submitted class and method match the required signature.";
    private const string TruncatedMarker = "\n[compiler diagnostics truncated]";

    public static string Sanitize(
        string diagnostics,
        string workDirectory,
        bool hideGeneratedHarnessDiagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentException.ThrowIfNullOrWhiteSpace(workDirectory);

        var normalized = diagnostics
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        normalized = RemoveUnsafeControlCharacters(normalized);
        normalized = ReplaceWorkDirectory(normalized, workDirectory);

        if (hideGeneratedHarnessDiagnostics)
            normalized = KeepSubmissionDiagnostics(normalized);

        normalized = normalized.Trim();
        if (normalized.Length == 0)
            normalized = GenericFunctionMessage;

        return TruncateUtf8(normalized, SubmissionContractLimits.MaxCompileMessageBytes);
    }

    private static string ReplaceWorkDirectory(string value, string workDirectory)
    {
        var normalizedDirectory = Path.GetFullPath(workDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var candidates = new[]
        {
            normalizedDirectory,
            normalizedDirectory.Replace('\\', '/'),
            "/workspace"
        };

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            value = value.Replace(
                $"{candidate}/solution.cpp",
                SubmissionFileName,
                StringComparison.OrdinalIgnoreCase);
            value = value.Replace(
                $"{candidate}\\solution.cpp",
                SubmissionFileName,
                StringComparison.OrdinalIgnoreCase);
        }

        return value;
    }

    private static string KeepSubmissionDiagnostics(string value)
    {
        var lines = value.Split('\n');
        var kept = new List<string>();
        var keepGroup = false;

        foreach (var line in lines)
        {
            if (line.Contains(GeneratedHarnessFileName, StringComparison.OrdinalIgnoreCase))
            {
                keepGroup = false;
                continue;
            }

            var header = DiagnosticHeader().Match(line);
            if (header.Success)
            {
                var fileName = header.Groups["file"].Value;
                keepGroup = fileName.EndsWith(SubmissionFileName, StringComparison.OrdinalIgnoreCase);
            }

            if (keepGroup)
                kept.Add(line);
        }

        return string.Join('\n', kept);
    }

    private static string RemoveUnsafeControlCharacters(string value)
    {
        var sanitized = new StringBuilder(value.Length);
        foreach (var rune in value.EnumerateRunes())
        {
            if (rune.Value is '\n' or '\t' || !Rune.IsControl(rune))
                sanitized.Append(rune.ToString());
        }

        return sanitized.ToString();
    }

    private static string TruncateUtf8(string value, int maximumBytes)
    {
        if (Encoding.UTF8.GetByteCount(value) <= maximumBytes)
            return value;

        var markerBytes = Encoding.UTF8.GetByteCount(TruncatedMarker);
        var availableBytes = maximumBytes - markerBytes;
        var truncated = new StringBuilder(value.Length);
        var usedBytes = 0;
        foreach (var rune in value.EnumerateRunes())
        {
            if (usedBytes + rune.Utf8SequenceLength > availableBytes)
                break;

            truncated.Append(rune.ToString());
            usedBytes += rune.Utf8SequenceLength;
        }

        return truncated.Append(TruncatedMarker).ToString();
    }

    [GeneratedRegex(
        "^(?<file>.+?):\\d+(?::\\d+)?:\\s+(?:fatal\\s+)?(?:error|warning|note):",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DiagnosticHeader();
}
