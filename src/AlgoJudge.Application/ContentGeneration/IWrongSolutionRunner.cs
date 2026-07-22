using AlgoJudge.Application.FunctionExecution;

namespace AlgoJudge.Application.ContentGeneration;

public interface IWrongSolutionRunner
{
    Task<IReadOnlySet<int>> FindKilledCasesAsync(
        string sourceCode,
        FunctionSignature signature,
        IReadOnlyList<string> inputs,
        IReadOnlyList<string> expectedOutputs,
        ReferenceSolutionLimits limits,
        CancellationToken cancellationToken = default);
}
