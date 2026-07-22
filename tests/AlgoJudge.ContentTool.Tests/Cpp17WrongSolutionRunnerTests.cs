using AlgoJudge.Application.FunctionExecution;
using AlgoJudge.Application.Interfaces;
using AlgoJudge.Application.ContentGeneration;
using AlgoJudge.ContentTool.Generation;
using AlgoJudge.Infrastructure.Grading;

namespace AlgoJudge.ContentTool.Tests;

public sealed class Cpp17WrongSolutionRunnerTests
{
    [Fact]
    public async Task MismatchedAndFailedCasesAreRecordedAsKilled()
    {
        var sandbox = new SequencedSandbox(
        [
            new SandboxRunResult { Status = SandboxRunStatus.Success, Output = "1" },
            new SandboxRunResult { Status = SandboxRunStatus.Success, Output = "wrong" },
            new SandboxRunResult { Status = SandboxRunStatus.RuntimeError }
        ]);
        var runner = new Cpp17WrongSolutionRunner(
            sandbox,
            new Cpp17FunctionHarnessBuilder());

        var killed = await runner.FindKilledCasesAsync(
            "class Solution { public: int solve(int value) { return value; } };",
            Signature(),
            ["{\"value\":1}", "{\"value\":2}", "{\"value\":3}"],
            ["1", "2", "3"],
            new ReferenceSolutionLimits(1000, 262144));

        Assert.Equal([2, 3], killed.Order());
    }

    [Fact]
    public async Task DeclaredWrongSolutionMustCompile()
    {
        var sandbox = new SequencedSandbox([]) { CompileSuccess = false };
        var runner = new Cpp17WrongSolutionRunner(
            sandbox,
            new Cpp17FunctionHarnessBuilder());

        var exception = await Assert.ThrowsAsync<TestGenerationException>(() =>
            runner.FindKilledCasesAsync(
                "invalid",
                Signature(),
                ["{\"value\":1}"],
                ["1"],
                new ReferenceSolutionLimits(1000, 262144)));

        Assert.Contains("did not compile", exception.Message, StringComparison.Ordinal);
    }

    private static FunctionSignature Signature() => new()
    {
        ClassName = "Solution",
        MethodName = "solve",
        ReturnType = FunctionValueType.Int32,
        Parameters =
        [
            new FunctionParameter { Name = "value", Type = FunctionValueType.Int32 }
        ]
    };

    private sealed class SequencedSandbox(IReadOnlyList<SandboxRunResult> results) : IDockerSandbox
    {
        private int _index;

        public bool CompileSuccess { get; init; } = true;

        public Task<SandboxCompileResult> CompileAsync(
            string sourceCode,
            string workDir,
            CancellationToken ct = default) =>
            Task.FromResult(new SandboxCompileResult { Success = CompileSuccess });

        public Task<SandboxRunResult> RunAsync(
            string workDir,
            string input,
            int timeLimitMs,
            int memoryLimitKb,
            CancellationToken ct = default) =>
            Task.FromResult(results[_index++]);
    }
}
