using AlgoJudge.Application.Contracts.Problems;
using AlgoJudge.Application.DTOs.Common;
using AlgoJudge.Application.DTOs.Submission;
using AlgoJudge.Application.Interfaces;
using AlgoJudge.Application.Models.SubmissionQueue;
using AlgoJudge.Application.Services;
using AlgoJudge.Domain.Entities;
using AlgoJudge.Domain.Enums;

namespace AlgoJudge.Application.Tests;

public class ProblemCatalogTests
{
    [Fact]
    public async Task AuthenticatedCatalogueDerivesSolvedStateFromAcceptedSubmissions()
    {
        var userId = Guid.NewGuid();
        var problems = new[]
        {
            CreatePublishedProblem(1, "two-sum"),
            CreatePublishedProblem(2, "valid-parentheses")
        };
        var problemRepository = new ProblemRepositoryStub(problems);
        var submissionRepository = new SubmissionRepositoryStub([2]);
        var service = new ProblemService(problemRepository, submissionRepository);

        var result = await service.GetProblemsAsync(new ProblemListQuery(), userId);

        Assert.False(result.Items.Single(problem => problem.Id == 1).IsSolved);
        Assert.True(result.Items.Single(problem => problem.Id == 2).IsSolved);
        Assert.Equal(new[] { 1, 2 }, submissionRepository.LastRequestedProblemIds);
    }

    [Fact]
    public async Task AnonymousCatalogueDoesNotExposeSolvedState()
    {
        var service = new ProblemService(
            new ProblemRepositoryStub([CreatePublishedProblem(1, "two-sum")]),
            new SubmissionRepositoryStub([]));

        var result = await service.GetProblemsAsync(new ProblemListQuery(), null);

        Assert.Null(Assert.Single(result.Items).IsSolved);
    }

    [Fact]
    public async Task SolvedFilterRequiresAuthentication()
    {
        var service = new ProblemService(
            new ProblemRepositoryStub([]),
            new SubmissionRepositoryStub([]));

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetProblemsAsync(new ProblemListQuery { Solved = true }, null));

        Assert.Contains("authenticated users", exception.Message);
    }

    [Fact]
    public async Task DetailMapsPublicSamplesWithoutJudgeTestCases()
    {
        var problem = CreatePublishedProblem(1, "two-sum");
        problem.Samples.Add(new ProblemSample
        {
            Id = 1,
            ProblemId = problem.Id,
            Input = "2 7 11 15\n9",
            ExpectedOutput = "0 1",
            Ordinal = 1
        });
        problem.JudgeTestCases.Add(new JudgeTestCase
        {
            Id = 99,
            ProblemId = problem.Id,
            Input = "hidden-input",
            ExpectedOutput = "hidden-output",
            Ordinal = 1
        });

        var service = new ProblemService(
            new ProblemRepositoryStub([problem]),
            new SubmissionRepositoryStub([]));

        var result = await service.GetProblemBySlugAsync(problem.Slug, null);

        var sample = Assert.Single(Assert.IsType<ProblemDetailResponse>(result).Samples);
        Assert.Equal("2 7 11 15\n9", sample.Input);
        Assert.DoesNotContain(
            typeof(ProblemDetailResponse).GetProperties(),
            property => property.Name.Contains("TestCase", StringComparison.OrdinalIgnoreCase) ||
                        property.Name.Contains("Hidden", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(ProblemStatus.Draft)]
    [InlineData(ProblemStatus.Archived)]
    public async Task SubmissionRejectsProblemsThatAreNotPublished(ProblemStatus status)
    {
        var problem = CreatePublishedProblem(1, "two-sum");
        problem.Status = status;
        var service = new SubmissionService(
            new SubmissionRepositoryStub([]),
            new ProblemRepositoryStub([problem]),
            null!,
            null!);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SubmitCodeAsync(
                new CreateSubmissionDto { ProblemId = problem.Id },
                Guid.NewGuid()));

        Assert.Contains("published problems", exception.Message);
    }

    private static Problem CreatePublishedProblem(int id, string slug)
    {
        return new Problem
        {
            Id = id,
            Slug = slug,
            Title = slug.Replace('-', ' '),
            StatementMarkdown = "Statement",
            ConstraintsMarkdown = "Constraints",
            TimeLimitMs = 1_000,
            MemoryLimitKb = 262_144,
            Difficulty = DifficultyLevel.Easy,
            Status = ProblemStatus.Published,
            PublishedAt = DateTime.UtcNow
        };
    }

    private sealed class ProblemRepositoryStub : IProblemRepository
    {
        private readonly IReadOnlyCollection<Problem> _problems;

        public ProblemRepositoryStub(IReadOnlyCollection<Problem> problems)
        {
            _problems = problems;
        }

        public Task<Problem?> GetByIdAsync(int id) =>
            Task.FromResult(_problems.SingleOrDefault(problem => problem.Id == id));

        public Task<Problem?> GetPublishedBySlugAsync(string slug) =>
            Task.FromResult(_problems.SingleOrDefault(problem =>
                problem.Status == ProblemStatus.Published && problem.Slug == slug));

        public Task<PagedResult<Problem>> GetPublishedPagedAsync(
            string? search,
            DifficultyLevel? difficulty,
            IReadOnlyCollection<string> tags,
            Guid? userId,
            bool? solved,
            int pageNumber,
            int pageSize)
        {
            return Task.FromResult(new PagedResult<Problem>
            {
                Items = _problems,
                TotalCount = _problems.Count,
                PageNumber = pageNumber,
                PageSize = pageSize
            });
        }
    }

    private sealed class SubmissionRepositoryStub : ISubmissionRepository
    {
        private readonly IReadOnlyCollection<int> _solvedProblemIds;

        public SubmissionRepositoryStub(IReadOnlyCollection<int> solvedProblemIds)
        {
            _solvedProblemIds = solvedProblemIds;
        }

        public int[] LastRequestedProblemIds { get; private set; } = [];

        public Task<IReadOnlyCollection<int>> GetSolvedProblemIdsAsync(
            Guid userId,
            IEnumerable<int> problemIds)
        {
            LastRequestedProblemIds = problemIds.ToArray();
            return Task.FromResult(_solvedProblemIds);
        }

        public Task<bool> HasAcceptedSubmissionAsync(Guid userId, int problemId) =>
            Task.FromResult(_solvedProblemIds.Contains(problemId));

        public Task AddAsync(Submission submission) => throw new NotSupportedException();
        public Task<Submission?> GetByIdAsync(Guid id) => throw new NotSupportedException();
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
        public Task<PagedResult<Submission>> GetPagedAsync(
            Guid userId,
            int? problemId,
            SubmissionStatus? status,
            int pageNumber,
            int pageSize) => throw new NotSupportedException();
    }
}
