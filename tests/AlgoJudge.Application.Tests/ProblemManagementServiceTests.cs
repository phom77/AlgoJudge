using AlgoJudge.Application.Contracts.Admin;
using AlgoJudge.Application.Exceptions;
using AlgoJudge.Application.Interfaces;
using AlgoJudge.Application.Models.Admin;
using AlgoJudge.Application.Models.Common;
using AlgoJudge.Application.Services;
using AlgoJudge.Domain.Enums;

namespace AlgoJudge.Application.Tests;

public sealed class ProblemManagementServiceTests
{
    [Fact]
    public async Task ArchiveTransitionsPublishedProblemToArchived()
    {
        var repository = new FakeRepository
        {
            Problem = Problem(ProblemStatus.Published),
            TransitionResult = true
        };
        var service = new ProblemManagementService(repository);

        await service.ArchiveAsync(42);

        Assert.Equal(42, repository.TransitionProblemId);
        Assert.Equal(ProblemStatus.Published, repository.ExpectedStatus);
        Assert.Equal(ProblemStatus.Archived, repository.NextStatus);
    }

    [Fact]
    public async Task RestoreRejectsProblemThatIsNotArchived()
    {
        var repository = new FakeRepository
        {
            Problem = Problem(ProblemStatus.Published),
            TransitionResult = false
        };
        var service = new ProblemManagementService(repository);

        await Assert.ThrowsAsync<ConflictException>(() => service.RestoreAsync(42));
    }

    [Fact]
    public async Task ListRejectsUndefinedStatus()
    {
        var service = new ProblemManagementService(new FakeRepository());

        await Assert.ThrowsAsync<RequestValidationException>(() => service.GetProblemsAsync(
            new AdminProblemListQuery { Status = (ProblemStatus)999 }));
    }

    private static AdminProblemDto Problem(ProblemStatus status) => new()
    {
        Id = 42,
        Slug = "maximum-array",
        Title = "Maximum Array",
        Status = status,
        Difficulty = DifficultyLevel.Easy,
        ExecutionMode = ProblemExecutionMode.Function,
        TimeLimitMs = 1000,
        MemoryLimitKb = 262144,
        JudgeVersion = 1,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private sealed class FakeRepository : IProblemManagementRepository
    {
        public AdminProblemDto? Problem { get; init; }
        public bool TransitionResult { get; init; }
        public int TransitionProblemId { get; private set; }
        public ProblemStatus ExpectedStatus { get; private set; }
        public ProblemStatus NextStatus { get; private set; }

        public Task<PagedResult<AdminProblemDto>> GetPagedAsync(
            string? search,
            ProblemStatus? status,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PagedResult<AdminProblemDto>
            {
                PageNumber = pageNumber,
                PageSize = pageSize
            });

        public Task<AdminProblemDto?> GetAsync(
            int problemId,
            CancellationToken cancellationToken = default) => Task.FromResult(Problem);

        public Task<bool> TransitionStatusAsync(
            int problemId,
            ProblemStatus expectedStatus,
            ProblemStatus nextStatus,
            CancellationToken cancellationToken = default)
        {
            TransitionProblemId = problemId;
            ExpectedStatus = expectedStatus;
            NextStatus = nextStatus;
            return Task.FromResult(TransitionResult);
        }
    }
}
