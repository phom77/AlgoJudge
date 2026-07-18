using AlgoJudge.Application.ContentGeneration;
using AlgoJudge.Application.Interfaces;

namespace AlgoJudge.ContentTool.Generation;

public sealed class Cpp17ReferenceSolutionRunner : IReferenceSolutionRunner
{
    private readonly IDockerSandbox _sandbox;

    public Cpp17ReferenceSolutionRunner(IDockerSandbox sandbox)
    {
        _sandbox = sandbox;
    }

    public async Task<IReadOnlyList<string>> RunAsync(
        string sourceCode,
        IReadOnlyList<string> inputs,
        ReferenceSolutionLimits limits,
        CancellationToken cancellationToken = default)
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
