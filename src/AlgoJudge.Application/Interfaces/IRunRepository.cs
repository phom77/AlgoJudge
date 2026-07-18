using AlgoJudge.Application.Models.RunQueue;
using AlgoJudge.Domain.Entities;
using AlgoJudge.Domain.Enums;

namespace AlgoJudge.Application.Interfaces;

public interface IRunRepository
{
    Task AddAsync(CodeRun run, CancellationToken cancellationToken = default);
    Task<CodeRun?> GetByIdForUserAsync(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CodeRun?> GetClaimedAsync(
        RunClaim claim,
        CancellationToken cancellationToken = default);
    Task<RunClaim?> ClaimNextAsync(
        string workerId,
        TimeSpan leaseDuration,
        int maxAttempts,
        CancellationToken cancellationToken = default);
    Task<bool> RenewLeaseAsync(
        RunClaim claim,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);
    Task<bool> FinalizeClaimAsync(
        RunClaim claim,
        RunStatus finalStatus,
        string? standardOutput,
        string? errorOutput,
        int executionTimeMs,
        int memoryUsedKb,
        CancellationToken cancellationToken = default);
    Task<bool> AbandonClaimAsync(
        RunClaim claim,
        int maxAttempts,
        CancellationToken cancellationToken = default);
}
