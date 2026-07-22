using AlgoJudge.Application.ContentGeneration;
using AlgoJudge.Application.FunctionExecution;
using AlgoJudge.Application.Interfaces;

namespace AlgoJudge.ContentTool.Generation;

public sealed class Cpp17WrongSolutionRunner : IWrongSolutionRunner
{
    private readonly IDockerSandbox _sandbox;
    private readonly IFunctionHarnessBuilder _functionHarnessBuilder;

    public Cpp17WrongSolutionRunner(
        IDockerSandbox sandbox,
        IFunctionHarnessBuilder functionHarnessBuilder)
    {
        _sandbox = sandbox;
        _functionHarnessBuilder = functionHarnessBuilder;
    }

    public async Task<IReadOnlySet<int>> FindKilledCasesAsync(
        string sourceCode,
        FunctionSignature signature,
        IReadOnlyList<string> inputs,
        IReadOnlyList<string> expectedOutputs,
        ReferenceSolutionLimits limits,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourceCode);
        ArgumentNullException.ThrowIfNull(signature);
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(expectedOutputs);
        if (inputs.Count != expectedOutputs.Count)
            throw new ArgumentException("Wrong-solution inputs and outputs must have equal counts.");

        var workDirectory = Path.Combine(
            Path.GetTempPath(),
            "algojudge-content-wrong-solution",
            Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(workDirectory);
            var harness = _functionHarnessBuilder.Build(sourceCode, signature);
            var compileResult = await _sandbox.CompileAsync(
                harness,
                workDirectory,
                cancellationToken);
            if (!compileResult.Success)
                throw new TestGenerationException("A declared wrong solution did not compile.");

            var killed = new HashSet<int>();
            for (var index = 0; index < inputs.Count; index++)
            {
                var result = await _sandbox.RunAsync(
                    workDirectory,
                    inputs[index],
                    limits.TimeLimitMs,
                    limits.MemoryLimitKb,
                    cancellationToken);
                if (result.Status == SandboxRunStatus.SystemError)
                    throw new TestGenerationException("Wrong-solution sandbox execution failed.");
                if (result.Status != SandboxRunStatus.Success ||
                    !string.Equals(
                        result.Output.Trim(),
                        expectedOutputs[index].Trim(),
                        StringComparison.Ordinal))
                {
                    killed.Add(index + 1);
                }
            }

            return killed;
        }
        finally
        {
            if (Directory.Exists(workDirectory))
                Directory.Delete(workDirectory, recursive: true);
        }
    }
}
