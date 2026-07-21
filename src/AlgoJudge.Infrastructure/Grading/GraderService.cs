using AlgoJudge.Application.FunctionExecution;
using AlgoJudge.Application.Interfaces;
using AlgoJudge.Application.Models.SubmissionQueue;
using AlgoJudge.Domain.Entities;
using AlgoJudge.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace AlgoJudge.Infrastructure.Grading
{
    public class GraderService : IGraderService
    {
        private readonly ISubmissionRepository _submissionRepository;
        private readonly IProblemRepository _problemRepository;
        private readonly ITestSuiteProvider _testSuiteProvider;
        private readonly IDockerSandbox _sandbox;
        private readonly IFunctionHarnessBuilder _functionHarnessBuilder;
        private readonly ILogger<GraderService> _logger;

        public GraderService(
            ISubmissionRepository submissionRepository,
            IProblemRepository problemRepository,
            ITestSuiteProvider testSuiteProvider,
            IDockerSandbox sandbox,
            IFunctionHarnessBuilder functionHarnessBuilder,
            ILogger<GraderService> logger)
        {
            _submissionRepository = submissionRepository;
            _problemRepository = problemRepository;
            _testSuiteProvider = testSuiteProvider;
            _sandbox = sandbox;
            _functionHarnessBuilder = functionHarnessBuilder;
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

            var suite = await _testSuiteProvider.GetSystemSuiteAsync(
                submission.ProblemId,
                submission.SystemTestSuiteVersion,
                cancellationToken);
            if (suite is null)
            {
                _logger.LogError(
                    "System suite {SuiteVersion} for problem {ProblemId} is unavailable for submission {SubmissionId}.",
                    submission.SystemTestSuiteVersion,
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
                $"submission-{submission.Id}-{claim.ClaimToken:N}");

            try
            {
                Directory.CreateDirectory(workDirectory);

                var sourceCode = BuildSourceCode(problem, submission.SourceCode);
                var compileResult = await _sandbox.CompileAsync(
                    sourceCode,
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

                foreach (var testCase in suite.TestCases)
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

                    if (runResult.Status == SandboxRunStatus.MemoryLimitExceeded)
                    {
                        finalStatus = SubmissionStatus.MemoryLimitExceeded;
                        break;
                    }

                    if (runResult.Status is
                        SandboxRunStatus.RuntimeError or
                        SandboxRunStatus.OutputLimitExceeded)
                    {
                        finalStatus = SubmissionStatus.RuntimeError;
                        break;
                    }

                    if (runResult.Status == SandboxRunStatus.SystemError)
                    {
                        throw new InvalidOperationException(
                            $"Sandbox system error while grading submission {submission.Id}.");
                    }

                    var memoryLimitBytes = (long)problem.MemoryLimitKb * 1024;
                    if (runResult.MemoryUsedBytes > memoryLimitBytes)
                    {
                        finalStatus = SubmissionStatus.MemoryLimitExceeded;
                        break;
                    }

                    var actualOutput = runResult.Output.Trim();
                    var expectedOutput = testCase.ExpectedOutput.Trim();
                    if (actualOutput != expectedOutput)
                    {
                        finalStatus = SubmissionStatus.WrongAnswer;
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

        private string BuildSourceCode(Problem problem, string submittedSource) =>
            problem.ExecutionMode switch
            {
                ProblemExecutionMode.StdinStdout => submittedSource,
                ProblemExecutionMode.Function => BuildFunctionSource(problem, submittedSource),
                _ => throw new InvalidOperationException(
                    $"Execution mode is invalid for problem {problem.Id}.")
            };

        private string BuildFunctionSource(Problem problem, string submittedSource)
        {
            var signature = FunctionSignatureJsonSerializer.Deserialize(
                problem.FunctionSignatureJson ?? throw new InvalidOperationException(
                    $"Function signature is missing for problem {problem.Id}."));

            return problem.FunctionAdapterTemplate is null
                ? _functionHarnessBuilder.Build(submittedSource, signature)
                : _functionHarnessBuilder.BuildLegacy(
                    submittedSource,
                    signature,
                    problem.FunctionAdapterTemplate);
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
