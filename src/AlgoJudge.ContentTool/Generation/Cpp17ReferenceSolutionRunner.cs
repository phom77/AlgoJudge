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

    private async Task<IReadOnlyList<string>> RunCoreAsync(
        string sourceCode,
        IReadOnlyList<string> inputs,
        ReferenceSolutionLimits limits,
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

            var outputs = new List<string>(inputs.Count);
            for (var index = 0; index < inputs.Count; index++)
            {
                var result = await _sandbox.RunAsync(
                    workDirectory,
                    inputs[index],
                    limits.TimeLimitMs,
                    limits.MemoryLimitKb,
                    cancellationToken);
                if (result.Status != SandboxRunStatus.Success)
                {
                    throw new TestGenerationException(
                        $"Reference solution failed for generated case {index + 1}: {result.Status}.");
                }

                outputs.Add(result.Output);
            }

            return outputs;
        }
        finally
        {
            if (Directory.Exists(workDirectory))
                Directory.Delete(workDirectory, recursive: true);
        }
    }
}
