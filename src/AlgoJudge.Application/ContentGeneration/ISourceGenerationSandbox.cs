namespace AlgoJudge.Application.ContentGeneration;

public interface ISourceGenerationSandbox
{
    Task<SourceGenerationResult> GenerateAsync(
        SourceGenerationRequest request,
        CancellationToken cancellationToken = default);
}
