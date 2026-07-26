using AlgoJudge.Application.ContentGeneration;
using AlgoJudge.Application.FunctionExecution;
using AlgoJudge.Application.Interfaces;
using AlgoJudge.Application.Models.ContentGeneration;

namespace AlgoJudge.Infrastructure.ContentGeneration;

public sealed class Cpp17ContentReferenceRunner : IFunctionReferenceSolutionRunner
{
    private readonly IDockerSandbox _sandbox;
    private readonly IFunctionHarnessBuilder _harnessBuilder;
    public Cpp17ContentReferenceRunner(IDockerSandbox sandbox, IFunctionHarnessBuilder harnessBuilder)
    {
        _sandbox = sandbox; _harnessBuilder = harnessBuilder;
    }

    public async Task<IReadOnlyList<string>> RunFunctionAsync(string sourceCode, FunctionSignature signature,
        IReadOnlyList<string> inputs, ReferenceSolutionLimits limits, CancellationToken cancellationToken = default)
    {
        var runs = await RunCompiledBatchesAsync(
            sourceCode,
            signature,
            inputs,
            limits,
            batchCount: 1,
            cancellationToken);
        return runs[0];
    }

    public async Task<(
        IReadOnlyList<string> First,
        IReadOnlyList<string> Repeated)> RunFunctionTwiceAsync(
            string sourceCode,
            FunctionSignature signature,
            IReadOnlyList<string> inputs,
            ReferenceSolutionLimits limits,
            CancellationToken cancellationToken = default)
    {
        var runs = await RunCompiledBatchesAsync(
            sourceCode,
            signature,
            inputs,
            limits,
            batchCount: 2,
            cancellationToken);
        return (runs[0], runs[1]);
    }

    private async Task<IReadOnlyList<IReadOnlyList<string>>> RunCompiledBatchesAsync(
        string sourceCode,
        FunctionSignature signature,
        IReadOnlyList<string> inputs,
        ReferenceSolutionLimits limits,
        int batchCount,
        CancellationToken cancellationToken)
    {
        var directory = Path.Combine(Path.GetTempPath(), "algojudge-content-reference", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(directory);
            var compile = await _sandbox.CompileAsync(_harnessBuilder.Build(sourceCode, signature), directory, cancellationToken);
            if (!compile.Success) throw new ContentGenerationException("reference_compile_error", "The reference solution did not compile.");

            var batches = new List<IReadOnlyList<string>>(batchCount);
            for (var batchIndex = 0; batchIndex < batchCount; batchIndex++)
            {
                var runs = await _sandbox.RunBatchAsync(
                    directory,
                    inputs,
                    limits.TimeLimitMs,
                    limits.MemoryLimitKb,
                    cancellationToken);
                if (runs.Count != inputs.Count)
                    throw new ContentGenerationException(
                        "reference_execution_error",
                        $"The reference solution failed on case {runs.Count}.");

                var outputs = new List<string>(runs.Count);
                for (var index = 0; index < runs.Count; index++)
                {
                    var run = runs[index];
                    if (run.Status != SandboxRunStatus.Success)
                        throw new ContentGenerationException("reference_execution_error", $"The reference solution failed on case {index + 1}.");
                    outputs.Add(run.Output);
                }
                batches.Add(outputs);
            }
            return batches;
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
