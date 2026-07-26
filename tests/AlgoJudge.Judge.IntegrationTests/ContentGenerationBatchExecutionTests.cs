using AlgoJudge.Application.ContentGeneration;
using AlgoJudge.Application.FunctionExecution;
using AlgoJudge.Application.Interfaces;
using AlgoJudge.Domain.Execution;
using AlgoJudge.Infrastructure.ContentGeneration;
using AlgoJudge.Infrastructure.Grading;

namespace AlgoJudge.Judge.IntegrationTests;

[Collection(DockerJudgeCollection.Name)]
public sealed class ContentGenerationBatchExecutionTests
{
    [DockerJudgeFact]
    public async Task ReferenceAndWrongSolutionUseOneBatchForOneThousandCases()
    {
        var sandbox = new CountingBatchSandbox(JudgeTestHarness.CreateSandbox());
        var signature = new FunctionSignature
        {
            ClassName = "Solution",
            MethodName = "solve",
            ReturnType = FunctionValueType.Int32,
            Parameters =
            [
                new FunctionParameter
                {
                    Name = "value",
                    Type = FunctionValueType.Int32
                }
            ]
        };
        var inputs = Enumerable.Range(0, 1_000)
            .Select(value => $"{{\"value\":{value}}}")
            .ToArray();
        var limits = new ReferenceSolutionLimits(1_000, 64 * 1024);
        var referenceRunner = new Cpp17ContentReferenceRunner(
            sandbox,
            new Cpp17FunctionHarnessBuilder());

        var (outputs, repeated) = await referenceRunner.RunFunctionTwiceAsync(
            """
            class Solution {
            public:
                int solve(int value) { return value * 2; }
            };
            """,
            signature,
            inputs,
            limits);

        Assert.Equal(1_000, outputs.Count);
        Assert.Equal("0", outputs[0]);
        Assert.Equal("1998", outputs[^1]);
        Assert.Equal(outputs, repeated);
        Assert.Equal(2, sandbox.StoppingBatchCount);
        Assert.Equal(1, sandbox.CompileCount);

        var wrongRunner = new Cpp17ContentWrongSolutionRunner(
            sandbox,
            new Cpp17FunctionHarnessBuilder(),
            new OutputChecker());
        var killed = await wrongRunner.FindKilledCasesAsync(
            """
            #include <cstdlib>
            class Solution {
            public:
                int solve(int value) {
                    if (value == 500) std::abort();
                    return value * 2;
                }
            };
            """,
            signature,
            inputs,
            outputs,
            limits,
            OutputCheckerConfiguration.JsonExact);

        Assert.Equal([501], killed.Order());
        Assert.Equal(1, sandbox.ContinuingBatchCount);
        Assert.Equal(2, sandbox.CompileCount);
        Assert.Equal(0, sandbox.SingleRunCount);
    }

    private sealed class CountingBatchSandbox(IDockerSandbox inner) : IDockerSandbox
    {
        public int CompileCount { get; private set; }
        public int SingleRunCount { get; private set; }
        public int StoppingBatchCount { get; private set; }
        public int ContinuingBatchCount { get; private set; }

        public Task<SandboxCompileResult> CompileAsync(
            string sourceCode,
            string workDir,
            CancellationToken ct = default)
        {
            CompileCount++;
            return inner.CompileAsync(sourceCode, workDir, ct);
        }

        public Task<SandboxRunResult> RunAsync(
            string workDir,
            string input,
            int timeLimitMs,
            int memoryLimitKb,
            CancellationToken ct = default)
        {
            SingleRunCount++;
            return inner.RunAsync(workDir, input, timeLimitMs, memoryLimitKb, ct);
        }

        public Task<IReadOnlyList<SandboxRunResult>> RunBatchAsync(
            string workDir,
            IReadOnlyList<string> inputs,
            int timeLimitMs,
            int memoryLimitKb,
            CancellationToken ct = default)
        {
            StoppingBatchCount++;
            return inner.RunBatchAsync(
                workDir,
                inputs,
                timeLimitMs,
                memoryLimitKb,
                ct);
        }

        public Task<IReadOnlyList<SandboxRunResult>> RunBatchContinuingAfterFailureAsync(
            string workDir,
            IReadOnlyList<string> inputs,
            int timeLimitMs,
            int memoryLimitKb,
            CancellationToken ct = default)
        {
            ContinuingBatchCount++;
            return inner.RunBatchContinuingAfterFailureAsync(
                workDir,
                inputs,
                timeLimitMs,
                memoryLimitKb,
                ct);
        }
    }
}
