using AlgoJudge.Application.FunctionExecution;
using AlgoJudge.Application.Models.Common;
using AlgoJudge.Application.Interfaces;
using AlgoJudge.Application.Models.SubmissionQueue;
using AlgoJudge.Domain.Entities;
using AlgoJudge.Domain.Enums;
using AlgoJudge.Infrastructure.Grading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AlgoJudge.Judge.IntegrationTests;

internal static class JudgeTestHarness
{
    public static DockerSandboxService CreateSandbox(
        int stdoutLimitBytes = 64 * 1024,
        int stderrLimitBytes = 64 * 1024)
    {
        var image = Environment.GetEnvironmentVariable(
            DockerJudgeFactAttribute.ImageEnvironmentVariable)
            ?? throw new InvalidOperationException("Docker judge image is not configured.");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Sandbox:DockerImage"] = image,
                ["Sandbox:CompileTimeoutSeconds"] = "30",
                ["Sandbox:DockerStartupAllowanceSeconds"] = "15",
                ["Sandbox:StdoutLimitBytes"] = stdoutLimitBytes.ToString(),
                ["Sandbox:StderrLimitBytes"] = stderrLimitBytes.ToString(),
                ["Sandbox:PidsLimit"] = "32"
            })
            .Build();

        return new DockerSandboxService(
            configuration,
            NullLogger<DockerSandboxService>.Instance);
    }

    public static async Task<JudgeOutcome> GradeAsync(
        string sourceCode,
        string input,
        string expectedOutput,
        int timeLimitMs = 1_000,
        int memoryLimitKb = 64 * 1024)
    {
        return await GradeWithSandboxAsync(
            sourceCode,
            input,
            expectedOutput,
            CreateSandbox(),
            NullLogger<GraderService>.Instance,
            timeLimitMs,
            memoryLimitKb);
    }

    public static async Task<JudgeOutcome> GradeWithSandboxAsync(
        string sourceCode,
        string input,
        string expectedOutput,
        IDockerSandbox sandbox,
        ILogger<GraderService> logger,
        int timeLimitMs = 1_000,
        int memoryLimitKb = 64 * 1024,
        ProblemExecutionMode executionMode = ProblemExecutionMode.StdinStdout,
        string? functionSignatureJson = null,
        string? functionAdapterTemplate = null)
    {
        var submissionId = Guid.NewGuid();
        var claimToken = Guid.NewGuid();
        var submission = new Submission
        {
            Id = submissionId,
            ProblemId = 1,
            UserId = Guid.NewGuid(),
            SourceCode = sourceCode,
            Language = "cpp17",
            Status = SubmissionStatus.Running,
            WorkerId = "judge-integration-test",
            ClaimToken = claimToken,
            LeaseExpiresAt = DateTime.UtcNow.AddMinutes(5),
            AttemptCount = 1
        };
        var problem = new Problem
        {
            Id = 1,
            Slug = "judge-integration-test",
            Title = "Judge integration test",
            StatementMarkdown = "Test",
            ConstraintsMarkdown = "Test",
            Difficulty = DifficultyLevel.Easy,
            Status = ProblemStatus.Published,
            TimeLimitMs = timeLimitMs,
            MemoryLimitKb = memoryLimitKb,
            ExecutionMode = executionMode,
            FunctionSignatureJson = functionSignatureJson,
            FunctionAdapterTemplate = functionAdapterTemplate
        };
        var testCase = new JudgeTestCase
        {
            Id = 1,
            ProblemId = problem.Id,
            Input = input,
            ExpectedOutput = expectedOutput,
            Ordinal = 1
        };
        var claim = new SubmissionClaim(
            submissionId,
            claimToken,
            submission.WorkerId,
            submission.AttemptCount,
            submission.LeaseExpiresAt.Value);
        var submissionRepository = new CapturingSubmissionRepository(submission);
        var grader = new GraderService(
            submissionRepository,
            new StubProblemRepository(problem),
            new StubJudgeTestCaseRepository(testCase),
            sandbox,
            new Cpp17FunctionHarnessBuilder(),
            logger);

        await grader.GradeAsync(claim);

        Assert.Equal(1, submissionRepository.FinalizationCount);
        Assert.NotNull(submissionRepository.FinalStatus);
        return new JudgeOutcome(
            submissionRepository.FinalStatus.Value,
            submissionRepository.ExecutionTimeMs,
            submissionRepository.MemoryUsedKb);
    }

    internal sealed record JudgeOutcome(
        SubmissionStatus Status,
        int ExecutionTimeMs,
        int MemoryUsedKb);

    private sealed class CapturingSubmissionRepository : ISubmissionRepository
    {
        private readonly Submission _submission;

        public CapturingSubmissionRepository(Submission submission)
        {
            _submission = submission;
        }

        public SubmissionStatus? FinalStatus { get; private set; }
        public int ExecutionTimeMs { get; private set; }
        public int MemoryUsedKb { get; private set; }
        public int FinalizationCount { get; private set; }

        public Task<Submission?> GetClaimedAsync(
            SubmissionClaim claim,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Submission?>(_submission);
        }

        public Task<bool> FinalizeClaimAsync(
            SubmissionClaim claim,
            SubmissionStatus finalStatus,
            int executionTimeMs,
            int memoryUsedKb,
            CancellationToken cancellationToken = default)
        {
            FinalStatus = finalStatus;
            ExecutionTimeMs = executionTimeMs;
            MemoryUsedKb = memoryUsedKb;
            FinalizationCount++;
            return Task.FromResult(true);
        }

        public Task AddAsync(Submission submission) => throw new NotSupportedException();
        public Task<Submission?> GetByIdForUserAsync(
            Guid id,
            Guid userId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> ExistsAsync(
            Guid id,
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
        public Task<bool> AbandonClaimAsync(
            SubmissionClaim claim,
            int maxAttempts,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<int>> GetSolvedProblemIdsAsync(
            Guid userId,
            IEnumerable<int> problemIds) => throw new NotSupportedException();
        public Task<bool> HasAcceptedSubmissionAsync(
            Guid userId,
            int problemId) => throw new NotSupportedException();
        public Task<PagedResult<Submission>> GetPagedAsync(
            Guid userId,
            int? problemId,
            SubmissionStatus? status,
            int pageNumber,
            int pageSize) => throw new NotSupportedException();
    }

    private sealed class StubProblemRepository : IProblemRepository
    {
        private readonly Problem _problem;

        public StubProblemRepository(Problem problem)
        {
            _problem = problem;
        }

        public Task<Problem?> GetByIdAsync(int id) => Task.FromResult<Problem?>(_problem);
        public Task<Problem?> GetPublishedBySlugAsync(string slug) =>
            throw new NotSupportedException();
        public Task<PagedResult<Problem>> GetPublishedPagedAsync(
            string? search,
            DifficultyLevel? difficulty,
            IReadOnlyCollection<string> tags,
            Guid? userId,
            bool? solved,
            int pageNumber,
            int pageSize) => throw new NotSupportedException();
    }

    private sealed class StubJudgeTestCaseRepository : IJudgeTestCaseRepository
    {
        private readonly JudgeTestCase _testCase;

        public StubJudgeTestCaseRepository(JudgeTestCase testCase)
        {
            _testCase = testCase;
        }

        public Task<IEnumerable<JudgeTestCase>> GetByProblemIdAsync(int problemId)
        {
            return Task.FromResult<IEnumerable<JudgeTestCase>>([_testCase]);
        }
    }
}
