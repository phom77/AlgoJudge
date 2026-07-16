using AlgoJudge.Application.Interfaces;
using AlgoJudge.Domain.Enums;
using AlgoJudge.Infrastructure.Grading;
using Microsoft.Extensions.Logging;

namespace AlgoJudge.Judge.IntegrationTests;

public class SensitiveDataLoggingTests
{
    private const string SourceSentinel = "source-sentinel-4aa181";
    private const string HiddenInputSentinel = "hidden-input-sentinel-208a15";
    private const string HiddenOutputSentinel = "hidden-output-sentinel-f75f3c";
    private const string ContestantOutputSentinel = "contestant-output-sentinel-028bd4";
    private const string StderrSentinel = "stderr-sentinel-b1d945";

    [Fact]
    public async Task GradingLogsContainIdentifiersButNoSensitivePayloads()
    {
        var logger = new CapturingLogger<GraderService>();
        var sandbox = new CapturingSandbox();

        var outcome = await JudgeTestHarness.GradeWithSandboxAsync(
            SourceSentinel,
            HiddenInputSentinel,
            HiddenOutputSentinel,
            sandbox,
            logger);

        Assert.Equal(SubmissionStatus.WrongAnswer, outcome.Status);
        Assert.Equal(SourceSentinel, sandbox.ReceivedSourceCode);
        Assert.Equal(HiddenInputSentinel, sandbox.ReceivedInput);
        var logs = string.Join(Environment.NewLine, logger.Entries);
        Assert.DoesNotContain(SourceSentinel, logs, StringComparison.Ordinal);
        Assert.DoesNotContain(HiddenInputSentinel, logs, StringComparison.Ordinal);
        Assert.DoesNotContain(HiddenOutputSentinel, logs, StringComparison.Ordinal);
        Assert.DoesNotContain(ContestantOutputSentinel, logs, StringComparison.Ordinal);
        Assert.DoesNotContain(StderrSentinel, logs, StringComparison.Ordinal);
    }

    private sealed class CapturingSandbox : IDockerSandbox
    {
        public string? ReceivedSourceCode { get; private set; }
        public string? ReceivedInput { get; private set; }

        public Task<SandboxCompileResult> CompileAsync(
            string sourceCode,
            string workDir,
            CancellationToken ct = default)
        {
            ReceivedSourceCode = sourceCode;
            return Task.FromResult(new SandboxCompileResult { Success = true });
        }

        public Task<SandboxRunResult> RunAsync(
            string workDir,
            string input,
            int timeLimitMs,
            int memoryLimitKb,
            CancellationToken ct = default)
        {
            ReceivedInput = input;
            return Task.FromResult(new SandboxRunResult
            {
                Status = SandboxRunStatus.Success,
                Output = ContestantOutputSentinel,
                ErrorOutput = StderrSentinel,
                ExecutionTimeMs = 1,
                MemoryUsedBytes = 1024
            });
        }
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(formatter(state, exception));
            if (exception is not null)
                Entries.Add(exception.ToString());
        }
    }
}
