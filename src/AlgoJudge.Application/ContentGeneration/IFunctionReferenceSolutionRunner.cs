using AlgoJudge.Application.FunctionExecution;

namespace AlgoJudge.Application.ContentGeneration;

public interface IFunctionReferenceSolutionRunner
{
    Task<IReadOnlyList<string>> RunFunctionAsync(
        string sourceCode,
        FunctionSignature signature,
        IReadOnlyList<string> inputs,
        ReferenceSolutionLimits limits,
        CancellationToken cancellationToken = default);
}
