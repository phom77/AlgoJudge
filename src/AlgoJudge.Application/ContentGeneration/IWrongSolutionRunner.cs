using AlgoJudge.Application.FunctionExecution;
using AlgoJudge.Domain.Execution;

namespace AlgoJudge.Application.ContentGeneration;

public interface IWrongSolutionRunner
{
    Task<IReadOnlySet<int>> FindKilledCasesAsync(
        string sourceCode,
        FunctionSignature signature,
        IReadOnlyList<string> inputs,
        IReadOnlyList<string> expectedOutputs,
        ReferenceSolutionLimits limits,
        OutputCheckerConfiguration outputChecker,
        CancellationToken cancellationToken = default);
}
