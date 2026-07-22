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
        var directory = Path.Combine(Path.GetTempPath(), "algojudge-content-reference", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(directory);
            var compile = await _sandbox.CompileAsync(_harnessBuilder.Build(sourceCode, signature), directory, cancellationToken);
            if (!compile.Success) throw new ContentGenerationException("reference_compile_error", "The reference solution did not compile.");
            var outputs = new List<string>(inputs.Count);
            for (var index = 0; index < inputs.Count; index++)
            {
                var run = await _sandbox.RunAsync(directory, inputs[index], limits.TimeLimitMs, limits.MemoryLimitKb, cancellationToken);
                if (run.Status != SandboxRunStatus.Success)
                    throw new ContentGenerationException("reference_execution_error", $"The reference solution failed on case {index + 1}.");
                outputs.Add(run.Output);
            }
            return outputs;
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
