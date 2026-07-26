using AlgoJudge.Application.Interfaces;
using AlgoJudge.Domain.Entities;
using AlgoJudge.Domain.Enums;
using AlgoJudge.Domain.Execution;
using AlgoJudge.Infrastructure.Grading;
using Microsoft.Extensions.Logging.Abstractions;

namespace AlgoJudge.Judge.IntegrationTests;

public sealed class SystemSuiteGradingTests
{
    [Fact]
    public async Task SubmissionExecutesEveryCaseFromItsPinnedSuiteVersion()
    {
        var sandbox = new SequencedSandbox(["2", "4", "6"]);
        var cases = Enumerable.Range(1, 3).Select(index => new JudgeTestCase
        {
            Id = index,
            ProblemId = 1,
            SystemTestSuiteVersion = 7,
            Ordinal = index,
            Input = index.ToString(),
            ExpectedOutput = (index * 2).ToString()
        }).ToArray();

        var outcome = await JudgeTestHarness.GradeWithSandboxAsync(
            "int main() {}", "unused", "unused", sandbox,
            NullLogger<GraderService>.Instance,
            systemTestSuiteVersion: 7,
            systemTestCases: cases);

        Assert.Equal(SubmissionStatus.Accepted, outcome.Status);
        Assert.Equal(["1", "2", "3"], sandbox.Inputs);
        Assert.Equal(1, sandbox.BatchCount);
    }

    [Fact]
    public async Task SubmissionReportsFirstWrongAnswerInStableOrdinalOrder()
    {
        var sandbox = new SequencedSandbox(["2", "wrong", "6"]);
        var cases = new[] { 3, 1, 2 }.Select(ordinal => new JudgeTestCase
        {
            Id = ordinal,
            ProblemId = 1,
            SystemTestSuiteVersion = 4,
            Ordinal = ordinal,
            Input = ordinal.ToString(),
            ExpectedOutput = (ordinal * 2).ToString()
        }).ToArray();

        var outcome = await JudgeTestHarness.GradeWithSandboxAsync(
            "int main() {}", "unused", "unused", sandbox,
            NullLogger<GraderService>.Instance,
            systemTestSuiteVersion: 4,
            systemTestCases: cases);

        Assert.Equal(SubmissionStatus.WrongAnswer, outcome.Status);
        Assert.Equal(["1", "2", "3"], sandbox.Inputs);
        Assert.Equal(1, sandbox.BatchCount);
    }

    [Fact]
    public async Task MissingPinnedSuiteVersionFinalizesAsSafeRuntimeErrorWithoutExecution()
    {
        var sandbox = new SequencedSandbox([]);
        var differentVersion = new JudgeTestCase
        {
            Id = 1, ProblemId = 1, SystemTestSuiteVersion = 1,
            Ordinal = 1, Input = "hidden", ExpectedOutput = "hidden"
        };

        var outcome = await JudgeTestHarness.GradeWithSandboxAsync(
            "int main() {}", "unused", "unused", sandbox,
            NullLogger<GraderService>.Instance,
            systemTestSuiteVersion: 2,
            systemTestCases: [differentVersion]);

        Assert.Equal(SubmissionStatus.RuntimeError, outcome.Status);
        Assert.Empty(sandbox.Inputs);
    }

    [Fact]
    public async Task SubmissionUsesThePinnedSuiteOutputChecker()
    {
        var sandbox = new SequencedSandbox([" [ 1, 2 ] \n"]);
        var outcome = await JudgeTestHarness.GradeWithSandboxAsync(
            "int main() {}", "unused", "unused", sandbox,
            NullLogger<GraderService>.Instance,
            systemTestCases:
            [
                new JudgeTestCase
                {
                    Id = 1, ProblemId = 1, SystemTestSuiteVersion = 1,
                    Ordinal = 1, Input = "input", ExpectedOutput = "[1,2]"
                }
            ],
            outputChecker: OutputCheckerConfiguration.JsonExact);

        Assert.Equal(SubmissionStatus.Accepted, outcome.Status);
    }

    private sealed class SequencedSandbox(IReadOnlyList<string> outputs) : IDockerSandbox
    {
        private int _index;
        public List<string> Inputs { get; } = [];
        public int BatchCount { get; private set; }
        public Task<SandboxCompileResult> CompileAsync(string sourceCode, string workDir, CancellationToken ct = default) =>
            Task.FromResult(new SandboxCompileResult { Success = true });
        public Task<SandboxRunResult> RunAsync(string workDir, string input, int timeLimitMs, int memoryLimitKb, CancellationToken ct = default)
        {
            Inputs.Add(input);
            return Task.FromResult(new SandboxRunResult
            {
                Status = SandboxRunStatus.Success,
                Output = outputs[_index++],
                ExecutionTimeMs = 1,
                MemoryUsedBytes = 1024
            });
        }

        public async Task<IReadOnlyList<SandboxRunResult>> RunBatchAsync(
            string workDir,
            IReadOnlyList<string> inputs,
            int timeLimitMs,
            int memoryLimitKb,
            CancellationToken ct = default)
        {
            BatchCount++;
            var results = new List<SandboxRunResult>(inputs.Count);
            foreach (var input in inputs)
                results.Add(await RunAsync(workDir, input, timeLimitMs, memoryLimitKb, ct));
            return results;
        }
    }
}
