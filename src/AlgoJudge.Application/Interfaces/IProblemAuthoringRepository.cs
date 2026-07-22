using AlgoJudge.Domain.Entities;

namespace AlgoJudge.Application.Interfaces;

public interface IProblemAuthoringRepository
{
    Task AddProblemAsync(Problem problem, CancellationToken cancellationToken = default);
    Task AddRevisionAsync(ProblemAuthoringRevision revision, CancellationToken cancellationToken = default);
    Task<ProblemAuthoringRevision?> GetOwnedRevisionAsync(
        Guid revisionId,
        Guid ownerUserId,
        bool includeCandidate = false,
        CancellationToken cancellationToken = default);
    Task<ProblemAuthoringRevision?> GetLatestOwnedRevisionAsync(
        int problemId,
        Guid ownerUserId,
        CancellationToken cancellationToken = default);
    Task<ContentGenerationJob?> GetLatestJobAsync(
        Guid revisionId,
        CancellationToken cancellationToken = default);
    Task DeleteCandidateCasesAsync(Guid revisionId, CancellationToken cancellationToken = default);
    Task<bool> PublishAsync(Guid revisionId, Guid ownerUserId, CancellationToken cancellationToken = default);
}
