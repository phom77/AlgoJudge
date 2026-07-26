using AlgoJudge.Application.ContentGeneration;
using AlgoJudge.Application.FunctionExecution;
using AlgoJudge.Application.Interfaces;

namespace AlgoJudge.ContentTool.Generation;

public sealed class Cpp17ReferenceSolutionRunner :
    IReferenceSolutionRunner,
    IFunctionReferenceSolutionRunner
{
    private readonly IDockerSandbox _sandbox;
    private readonly IFunctionHarnessBuilder _functionHarnessBuilder;

    public Cpp17ReferenceSolutionRunner(
        IDockerSandbox sandbox,
        IFunctionHarnessBuilder functionHarnessBuilder)
    {
        _sandbox = sandbox;
        _functionHarnessBuilder = functionHarnessBuilder;
    }

    public async Task<IReadOnlyList<string>> RunAsync(
        string sourceCode,
        IReadOnlyList<string> inputs,
        ReferenceSolutionLimits limits,
        CancellationToken cancellationToken = default)
    {
        return await RunCoreAsync(
            sourceCode,
            inputs,
            limits,
            cancellationToken);
    }

    public async Task<IReadOnlyList<string>> RunFunctionAsync(
        string sourceCode,
        FunctionSignature signature,
        IReadOnlyList<string> inputs,
        ReferenceSolutionLimits limits,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourceCode);
        ArgumentNullException.ThrowIfNull(signature);
        var harness = _functionHarnessBuilder.Build(sourceCode, signature);
        return await RunCoreAsync(harness, inputs, limits, cancellationToken);
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
        ArgumentNullException.ThrowIfNull(sourceCode);
        ArgumentNullException.ThrowIfNull(signature);
        var harness = _functionHarnessBuilder.Build(sourceCode, signature);
        var batches = await RunCoreBatchesAsync(
            harness,
            inputs,
            limits,
            batchCount: 2,
            cancellationToken);
        return (batches[0], batches[1]);
    }

    private async Task<IReadOnlyList<string>> RunCoreAsync(
        string sourceCode,
        IReadOnlyList<string> inputs,
        ReferenceSolutionLimits limits,
        CancellationToken cancellationToken)
    {
        var batches = await RunCoreBatchesAsync(
            sourceCode,
            inputs,
            limits,
            batchCount: 1,
            cancellationToken);
        return batches[0];
    }

    private async Task<IReadOnlyList<IReadOnlyList<string>>> RunCoreBatchesAsync(
        string sourceCode,
        IReadOnlyList<string> inputs,
        ReferenceSolutionLimits limits,
        int batchCount,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceCode);
        ArgumentNullException.ThrowIfNull(inputs);

        var workDirectory = Path.Combine(
            Path.GetTempPath(),
            "algojudge-content",
            Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(workDirectory);
            var compileResult = await _sandbox.CompileAsync(
                sourceCode,
                workDirectory,
                cancellationToken);
            if (!compileResult.Success)
            {
                throw new TestGenerationException(
                    $"Reference solution did not compile: {compileResult.ErrorOutput}");
            }

            var batches = new List<IReadOnlyList<string>>(batchCount);
            for (var batchIndex = 0; batchIndex < batchCount; batchIndex++)
            {
                var results = await _sandbox.RunBatchAsync(
                    workDirectory,
                    inputs,
                    limits.TimeLimitMs,
                    limits.MemoryLimitKb,
                    cancellationToken);
                if (results.Count != inputs.Count)
                {
                    throw new TestGenerationException(
                        $"Reference solution failed for generated case {results.Count}.");
                }

                var outputs = new List<string>(results.Count);
                for (var index = 0; index < results.Count; index++)
                {
                    var result = results[index];
                    if (result.Status != SandboxRunStatus.Success)
                    {
                        throw new TestGenerationException(
                            $"Reference solution failed for generated case {index + 1}: {result.Status}.");
                    }

                    outputs.Add(result.Output);
                }
                batches.Add(outputs);
            }

            return batches;
        }
        finally
        {
            if (Directory.Exists(workDirectory))
                Directory.Delete(workDirectory, recursive: true);
        }
    }
}
