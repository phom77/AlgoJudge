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

    async Task<(
        IReadOnlyList<string> First,
        IReadOnlyList<string> Repeated)> RunFunctionTwiceAsync(
            string sourceCode,
            FunctionSignature signature,
            IReadOnlyList<string> inputs,
            ReferenceSolutionLimits limits,
            CancellationToken cancellationToken = default)
    {
        var first = await RunFunctionAsync(
            sourceCode,
            signature,
            inputs,
            limits,
            cancellationToken);
        var repeated = await RunFunctionAsync(
            sourceCode,
            signature,
            inputs,
            limits,
            cancellationToken);
        return (first, repeated);
    }
}
