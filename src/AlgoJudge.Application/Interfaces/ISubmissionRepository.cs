using AlgoJudge.Application.Models.Common;
using AlgoJudge.Application.Models.SubmissionQueue;
using AlgoJudge.Domain.Entities;
using AlgoJudge.Domain.Enums;

namespace AlgoJudge.Application.Interfaces
{
    public interface ISubmissionRepository
    {
        Task AddAsync(Submission submission);
        Task<Submission?> GetByIdForUserAsync(
            Guid id,
            Guid userId,
            CancellationToken cancellationToken = default);
        Task<bool> ExistsAsync(
            Guid id,
            CancellationToken cancellationToken = default);
        Task<Submission?> GetClaimedAsync(
            SubmissionClaim claim,
            CancellationToken cancellationToken = default);
        Task<SubmissionClaim?> ClaimNextAsync(
            string workerId,
            TimeSpan leaseDuration,
            int maxAttempts,
            CancellationToken cancellationToken = default);
        Task<bool> RenewLeaseAsync(
            SubmissionClaim claim,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken = default);
        Task<bool> FinalizeClaimAsync(
            SubmissionClaim claim,
            SubmissionStatus finalStatus,
            int executionTimeMs,
            int memoryUsedKb,
            CancellationToken cancellationToken = default);
        Task<bool> AbandonClaimAsync(
            SubmissionClaim claim,
            int maxAttempts,
            CancellationToken cancellationToken = default);
        Task<IReadOnlyCollection<int>> GetSolvedProblemIdsAsync(
            Guid userId,
            IEnumerable<int> problemIds);
        Task<bool> HasAcceptedSubmissionAsync(Guid userId, int problemId);
        Task<PagedResult<Submission>> GetPagedAsync(
            Guid userId,
            int? problemId,
            SubmissionStatus? status,
            int pageNumber,
            int pageSize);
    }
}
