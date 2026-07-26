using AlgoJudge.Application.ContentGeneration;
using AlgoJudge.Application.FunctionExecution;
using AlgoJudge.Application.Interfaces;
using AlgoJudge.ContentTool.Generation;
using AlgoJudge.Infrastructure.Grading;

namespace AlgoJudge.ContentTool.Tests;

public sealed class Cpp17ReferenceSolutionRunnerTests
{
    [Fact]
    public async Task RunFunctionAsyncUsesGenericHarnessAndReturnsEveryOutput()
    {
        const string source =
            "class Solution { public: int solve(int value) { return value * 2; } };";
        var signature = new FunctionSignature
        {
            ClassName = "Solution",
            MethodName = "solve",
            ReturnType = FunctionValueType.Int32,
            Parameters =
            [
                new FunctionParameter { Name = "value", Type = FunctionValueType.Int32 }
            ]
        };
        var sandbox = new CapturingSandbox(["2", "4"]);
        var runner = new Cpp17ReferenceSolutionRunner(
            sandbox,
            new Cpp17FunctionHarnessBuilder());

        var outputs = await runner.RunFunctionAsync(
            source,
            signature,
            ["{\"value\":1}", "{\"value\":2}"],
            new ReferenceSolutionLimits(1000, 262144));

        Assert.Equal(["2", "4"], outputs);
        Assert.Contains(source, sandbox.CompiledSource, StringComparison.Ordinal);
        Assert.Contains(
            "solution.solve(argument_0)",
            sandbox.CompiledSource,
            StringComparison.Ordinal);
        Assert.Equal(["{\"value\":1}", "{\"value\":2}"], sandbox.Inputs);
        Assert.Equal(1, sandbox.BatchCalls);
    }

    [Fact]
    public async Task RunFunctionTwiceAsyncCompilesOnceAndUsesTwoBatches()
    {
        var sandbox = new CapturingSandbox(["2", "2"]);
        var runner = new Cpp17ReferenceSolutionRunner(
            sandbox,
            new Cpp17FunctionHarnessBuilder());

        var (first, repeated) = await runner.RunFunctionTwiceAsync(
            "class Solution { public: int solve(int value) { return value * 2; } };",
            new FunctionSignature
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
            },
            ["{\"value\":1}"],
            new ReferenceSolutionLimits(1000, 262144));

        Assert.Equal(["2"], first);
        Assert.Equal(["2"], repeated);
        Assert.Equal(1, sandbox.CompileCalls);
        Assert.Equal(2, sandbox.BatchCalls);
    }

    private sealed class CapturingSandbox(IReadOnlyList<string> outputs) : IDockerSandbox
    {
        private int _runIndex;

        public string? CompiledSource { get; private set; }
        public List<string> Inputs { get; } = [];
        public int CompileCalls { get; private set; }
        public int BatchCalls { get; private set; }

        public Task<SandboxCompileResult> CompileAsync(
            string sourceCode,
            string workDir,
            CancellationToken ct = default)
        {
            CompileCalls++;
            CompiledSource = sourceCode;
            return Task.FromResult(new SandboxCompileResult { Success = true });
        }

        public Task<SandboxRunResult> RunAsync(
            string workDir,
            string input,
            int timeLimitMs,
            int memoryLimitKb,
            CancellationToken ct = default)
        {
            Inputs.Add(input);
            return Task.FromResult(new SandboxRunResult
            {
                Status = SandboxRunStatus.Success,
                Output = outputs[_runIndex++],
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
            BatchCalls++;
            var results = new List<SandboxRunResult>(inputs.Count);
            foreach (var input in inputs)
                results.Add(await RunAsync(workDir, input, timeLimitMs, memoryLimitKb, ct));
            return results;
        }
    }
}
