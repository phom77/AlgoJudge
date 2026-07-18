namespace AlgoJudge.Application.ContentGeneration;

public interface IReferenceSolutionRunner
{
    Task<IReadOnlyList<string>> RunAsync(
        string sourceCode,
        IReadOnlyList<string> inputs,
        ReferenceSolutionLimits limits,
        CancellationToken cancellationToken = default);
}
