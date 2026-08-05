using System.Text;
using AlgoJudge.Application.Contracts.Submissions;
using AlgoJudge.Infrastructure.Grading;

namespace AlgoJudge.Judge.IntegrationTests;

public sealed class CompilerDiagnosticSanitizerTests
{
    [Fact]
    public void SanitizerRemovesHostPathsControlCharactersAndBoundsUtf8()
    {
        var workDirectory = Path.Combine(Path.GetTempPath(), "algojudge", "submission-sensitive");
        var diagnostic = $"{workDirectory}{Path.DirectorySeparatorChar}solution.cpp:1:1: error: bad\0" +
            new string('\u00E9', SubmissionContractLimits.MaxCompileMessageBytes);

        var result = CompilerDiagnosticSanitizer.Sanitize(
            diagnostic,
            workDirectory,
            hideGeneratedHarnessDiagnostics: false);

        Assert.StartsWith("submission.cpp:1:1: error: bad", result, StringComparison.Ordinal);
        Assert.DoesNotContain(workDirectory, result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain('\0', result);
        Assert.EndsWith("[compiler diagnostics truncated]", result, StringComparison.Ordinal);
        Assert.True(
            Encoding.UTF8.GetByteCount(result) <= SubmissionContractLimits.MaxCompileMessageBytes);
    }

    [Fact]
    public void FunctionSanitizerReturnsOnlySubmissionDiagnostics()
    {
        const string diagnostic =
            "submission.cpp:2:4: error: expected ';'\n" +
            "    2 | bad source\n" +
            "      |    ^\n" +
            "algojudge-harness.cpp:20:3: error: private adapter detail\n" +
            "   20 | hidden harness source\n" +
            "      | ^";

        var result = CompilerDiagnosticSanitizer.Sanitize(
            diagnostic,
            Path.GetTempPath(),
            hideGeneratedHarnessDiagnostics: true);

        Assert.Contains("submission.cpp:2:4", result, StringComparison.Ordinal);
        Assert.Contains("bad source", result, StringComparison.Ordinal);
        Assert.DoesNotContain("algojudge-harness", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private adapter", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hidden harness", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FunctionSanitizerUsesSafeGuidanceWhenOnlyHarnessDiagnosticsRemain()
    {
        const string diagnostic =
            "algojudge-harness.cpp:20:3: error: generated implementation detail\n" +
            "   20 | private harness source";

        var result = CompilerDiagnosticSanitizer.Sanitize(
            diagnostic,
            Path.GetTempPath(),
            hideGeneratedHarnessDiagnostics: true);

        Assert.Equal(
            "Compilation failed. Verify that the submitted class and method match the required signature.",
            result);
    }
}
