using AlgoJudge.Application.Models.ContentGeneration;

namespace AlgoJudge.Application.Interfaces;

public interface IContentGenerationJobRepository
{
    Task<ContentGenerationClaim?> ClaimNextAsync(
        string workerId,
        TimeSpan leaseDuration,
        int maxAttempts,
        CancellationToken cancellationToken = default);
    Task<bool> RenewLeaseAsync(
        ContentGenerationClaim claim,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);
    Task<bool> CompleteAsync(
        ContentGenerationClaim claim,
        ContentGenerationResult result,
        CancellationToken cancellationToken = default);
    Task<bool> FailAsync(
        ContentGenerationClaim claim,
        string errorCode,
        string safeErrorMessage,
        CancellationToken cancellationToken = default);
    Task<bool> AbandonAsync(
        ContentGenerationClaim claim,
        int maxAttempts,
        CancellationToken cancellationToken = default);
}
