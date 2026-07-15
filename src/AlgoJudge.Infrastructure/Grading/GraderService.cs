using AlgoJudge.Application.Interfaces;
using AlgoJudge.Application.Models.SubmissionQueue;
using AlgoJudge.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace AlgoJudge.Infrastructure.Grading
{
    public class GraderService : IGraderService
    {
        private readonly ISubmissionRepository _submissionRepository;
        private readonly IProblemRepository _problemRepository;
        private readonly IJudgeTestCaseRepository _judgeTestCaseRepository;
        private readonly IDockerSandbox _sandbox;
        private readonly ILogger<GraderService> _logger;

        public GraderService(
            ISubmissionRepository submissionRepository,
            IProblemRepository problemRepository,
            IJudgeTestCaseRepository judgeTestCaseRepository,
            IDockerSandbox sandbox,
            ILogger<GraderService> logger)
        {
            _submissionRepository = submissionRepository;
            _problemRepository = problemRepository;
            _judgeTestCaseRepository = judgeTestCaseRepository;
            _sandbox = sandbox;
            _logger = logger;
        }

        public async Task GradeAsync(
            SubmissionClaim claim,
            CancellationToken cancellationToken = default)
        {
            var submission = await _submissionRepository.GetClaimedAsync(
                claim,
                cancellationToken);
            if (submission is null)
                throw new SubmissionClaimLostException(claim.SubmissionId);

            var problem = await _problemRepository.GetByIdAsync(submission.ProblemId);
            if (problem is null)
            {
                _logger.LogError(
                    "Problem {ProblemId} is missing for claimed submission {SubmissionId}.",
                    submission.ProblemId,
                    submission.Id);
                await FinalizeOrThrowAsync(
                    claim,
                    SubmissionStatus.RuntimeError,
                    executionTimeMs: 0,
                    memoryUsedKb: 0,
                    cancellationToken);
                return;
            }

            var testCases = (await _judgeTestCaseRepository
                .GetByProblemIdAsync(submission.ProblemId))
                .ToList();
            if (testCases.Count == 0)
            {
                _logger.LogError(
                    "Problem {ProblemId} has no judge test cases for submission {SubmissionId}.",
                    submission.ProblemId,
                    submission.Id);
                await FinalizeOrThrowAsync(
                    claim,
                    SubmissionStatus.RuntimeError,
                    executionTimeMs: 0,
                    memoryUsedKb: 0,
                    cancellationToken);
                return;
            }

            var workDirectory = Path.Combine(
                Path.GetTempPath(),
                "algojudge",
                submission.Id.ToString());

            try
            {
                Directory.CreateDirectory(workDirectory);

                var compileResult = await _sandbox.CompileAsync(
                    submission.SourceCode,
                    workDirectory,
                    cancellationToken);
                if (!compileResult.Success)
                {
                    await FinalizeOrThrowAsync(
                        claim,
                        SubmissionStatus.CompileError,
                        executionTimeMs: 0,
                        memoryUsedKb: 0,
                        cancellationToken);
                    return;
                }

                var finalStatus = SubmissionStatus.Accepted;
                var maxExecutionTime = 0;
                long maxMemoryUsedBytes = 0;

                foreach (var testCase in testCases)
                {
                    var runResult = await _sandbox.RunAsync(
                        workDirectory,
                        testCase.Input,
                        problem.TimeLimitMs,
                        problem.MemoryLimitKb,
                        cancellationToken);

                    maxExecutionTime = Math.Max(maxExecutionTime, runResult.ExecutionTimeMs);
                    maxMemoryUsedBytes = Math.Max(maxMemoryUsedBytes, runResult.MemoryUsedBytes);

                    if (runResult.Status == SandboxRunStatus.TimeLimitExceeded)
                    {
                        finalStatus = SubmissionStatus.TimeLimitExceeded;
                        break;
                    }

                    if (runResult.Status == SandboxRunStatus.RuntimeError)
                    {
                        finalStatus = SubmissionStatus.RuntimeError;
                        break;
                    }

                    if (runResult.Status == SandboxRunStatus.SystemError)
                    {
                        throw new InvalidOperationException(
                            $"Sandbox system error while grading submission {submission.Id}.");
                    }

                    var actualOutput = runResult.Output.Trim();
                    var expectedOutput = testCase.ExpectedOutput.Trim();
                    if (actualOutput != expectedOutput)
                    {
                        finalStatus = SubmissionStatus.WrongAnswer;
                        break;
                    }

                    var memoryLimitBytes = (long)problem.MemoryLimitKb * 1024;
                    if (runResult.MemoryUsedBytes > memoryLimitBytes)
                    {
                        finalStatus = SubmissionStatus.MemoryLimitExceeded;
                        break;
                    }
                }

                var memoryUsedKb = (int)Math.Min(int.MaxValue, maxMemoryUsedBytes / 1024);
                await FinalizeOrThrowAsync(
                    claim,
                    finalStatus,
                    maxExecutionTime,
                    memoryUsedKb,
                    cancellationToken);

                _logger.LogInformation(
                    "Submission {SubmissionId} finalized as {Status} on attempt {AttemptCount}.",
                    submission.Id,
                    finalStatus,
                    claim.AttemptCount);
            }
            finally
            {
                try
                {
                    if (Directory.Exists(workDirectory))
                        Directory.Delete(workDirectory, recursive: true);
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(
                        exception,
                        "Could not delete work directory for submission {SubmissionId}.",
                        submission.Id);
                }
            }
        }

        private async Task FinalizeOrThrowAsync(
            SubmissionClaim claim,
            SubmissionStatus finalStatus,
            int executionTimeMs,
            int memoryUsedKb,
            CancellationToken cancellationToken)
        {
            var finalized = await _submissionRepository.FinalizeClaimAsync(
                claim,
                finalStatus,
                executionTimeMs,
                memoryUsedKb,
                cancellationToken);
            if (!finalized)
                throw new SubmissionClaimLostException(claim.SubmissionId);
        }
    }
}
