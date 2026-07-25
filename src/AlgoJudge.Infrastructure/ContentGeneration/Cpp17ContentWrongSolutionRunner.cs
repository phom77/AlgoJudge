using AlgoJudge.Application.ContentGeneration;
using AlgoJudge.Application.FunctionExecution;
using AlgoJudge.Application.Interfaces;
using AlgoJudge.Application.Models.ContentGeneration;
using AlgoJudge.Domain.Execution;

namespace AlgoJudge.Infrastructure.ContentGeneration;

public sealed class Cpp17ContentWrongSolutionRunner : IWrongSolutionRunner
{
    private readonly IDockerSandbox _sandbox;
    private readonly IFunctionHarnessBuilder _harnessBuilder;
    private readonly IOutputChecker _outputChecker;
    public Cpp17ContentWrongSolutionRunner(IDockerSandbox sandbox, IFunctionHarnessBuilder harnessBuilder,
        IOutputChecker outputChecker)
    {
        _sandbox = sandbox; _harnessBuilder = harnessBuilder; _outputChecker = outputChecker;
    }

    public async Task<IReadOnlySet<int>> FindKilledCasesAsync(string sourceCode, FunctionSignature signature,
        IReadOnlyList<string> inputs, IReadOnlyList<string> expectedOutputs, ReferenceSolutionLimits limits,
        OutputCheckerConfiguration outputChecker,
        CancellationToken cancellationToken = default)
    {
        var directory = Path.Combine(Path.GetTempPath(), "algojudge-content-wrong", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(directory);
            var compile = await _sandbox.CompileAsync(_harnessBuilder.Build(sourceCode, signature), directory, cancellationToken);
            if (!compile.Success) throw new ContentGenerationException("wrong_solution_compile_error", "A declared wrong solution did not compile.");
            var killed = new HashSet<int>();
            for (var index = 0; index < inputs.Count; index++)
            {
                var run = await _sandbox.RunAsync(directory, inputs[index], limits.TimeLimitMs, limits.MemoryLimitKb, cancellationToken);
                if (run.Status == SandboxRunStatus.SystemError)
                    throw new ContentGenerationException("sandbox_error", "A wrong-solution sandbox failed.");
                if (run.Status != SandboxRunStatus.Success ||
                    !_outputChecker.IsMatch(outputChecker, expectedOutputs[index], run.Output))
                    killed.Add(index + 1);
            }
            return killed;
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
