using AlgoJudge.Application.Contracts.Auth;
using AlgoJudge.Application.Exceptions;
using AlgoJudge.Application.Interfaces;
using AlgoJudge.Application.Models.Common;
using AlgoJudge.Application.Models.SubmissionQueue;
using AlgoJudge.Application.Services;
using AlgoJudge.Domain.Entities;
using AlgoJudge.Domain.Enums;

namespace AlgoJudge.Application.Tests;

public class LegacyScopeTests
{
    [Fact]
    public void PublicAndDomainModelsDoNotExposeLegacyRoleOrScoreFields()
    {
        Assert.Null(typeof(RegisterRequest).GetProperty("Role"));
        Assert.Null(typeof(AuthResponse).GetProperty("Role"));
        Assert.Null(typeof(User).GetProperty("Role"));
        Assert.Null(typeof(Problem).GetProperty("Score"));
        Assert.Null(typeof(Problem).GetProperty("CreatedBy"));
        Assert.Null(typeof(Problem).GetProperty("Creator"));
    }

    [Fact]
    public async Task GetSubmissionByIdRejectsAnotherUser()
    {
        var ownerId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var submission = new Submission
        {
            Id = Guid.NewGuid(),
            UserId = ownerId,
            ProblemId = 1
        };

        var repository = new SubmissionRepositoryStub(submission);
        var service = new SubmissionService(
            repository,
            null!,
            null!,
            null!);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.GetSubmissionByIdAsync(submission.Id, requesterId));

        Assert.Equal(requesterId, repository.LastOwnedLookupUserId);
        Assert.True(repository.ExistenceChecked);
    }

    [Fact]
    public async Task GetSubmissionByIdReturnsNullWhenSubmissionDoesNotExist()
    {
        var repository = new SubmissionRepositoryStub(new Submission
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            ProblemId = 1
        });
        var service = new SubmissionService(repository, null!, null!, null!);

        var result = await service.GetSubmissionByIdAsync(
            Guid.NewGuid(),
            Guid.NewGuid());

        Assert.Null(result);
        Assert.True(repository.ExistenceChecked);
    }

    private sealed class SubmissionRepositoryStub : ISubmissionRepository
    {
        private readonly Submission _submission;

        public SubmissionRepositoryStub(Submission submission)
        {
            _submission = submission;
        }

        public Guid? LastOwnedLookupUserId { get; private set; }
        public bool ExistenceChecked { get; private set; }

        public Task AddAsync(Submission submission) => throw new NotSupportedException();

        public Task<Submission?> GetByIdForUserAsync(
            Guid id,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            LastOwnedLookupUserId = userId;
            return Task.FromResult<Submission?>(
                id == _submission.Id && userId == _submission.UserId
                    ? _submission
                    : null);
        }

        public Task<bool> ExistsAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            ExistenceChecked = true;
            return Task.FromResult(id == _submission.Id);
        }

        public Task<Submission?> GetClaimedAsync(
            SubmissionClaim claim,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<SubmissionClaim?> ClaimNextAsync(
            string workerId,
            TimeSpan leaseDuration,
            int maxAttempts,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<bool> RenewLeaseAsync(
            SubmissionClaim claim,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<bool> FinalizeClaimAsync(
            SubmissionClaim claim,
            SubmissionStatus finalStatus,
            int executionTimeMs,
            int memoryUsedKb,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<bool> AbandonClaimAsync(
            SubmissionClaim claim,
            int maxAttempts,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyCollection<int>> GetSolvedProblemIdsAsync(
            Guid userId,
            IEnumerable<int> problemIds) => throw new NotSupportedException();

        public Task<bool> HasAcceptedSubmissionAsync(Guid userId, int problemId) =>
            throw new NotSupportedException();

        public Task<PagedResult<Submission>> GetPagedAsync(
            Guid userId,
            int? problemId,
            SubmissionStatus? status,
            int pageNumber,
            int pageSize) => throw new NotSupportedException();
    }
}
