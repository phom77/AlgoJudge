using AlgoJudge.Application.Models.ContentGeneration;

namespace AlgoJudge.Application.Interfaces;

public interface IContentGenerationEngine
{
    Task<ContentGenerationResult> GenerateAsync(
        ContentGenerationClaim claim,
        CancellationToken cancellationToken = default);
}
