using AlgoJudge.Application.Interfaces;
using AlgoJudge.Domain.Enums;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace AlgoJudge.Infrastructure.Grading
{
    public class GraderService : IGraderService
    {
        private readonly ISubmissionRepository _submissionRepository;
        private readonly IProblemRepository _problemRepository;
        private readonly ITestCaseRepository _testCaseRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IDockerSandbox _sandbox;
        private readonly ILogger<GraderService> _logger;

        public GraderService(
            ISubmissionRepository submissionRepository,
            IProblemRepository problemRepository,
            ITestCaseRepository testCaseRepository,
            IUnitOfWork unitOfWork,
            IDockerSandbox sandbox,
            ILogger<GraderService> logger)
        {
            _submissionRepository = submissionRepository;
            _problemRepository = problemRepository;
            _testCaseRepository = testCaseRepository;
            _unitOfWork = unitOfWork;
            _sandbox = sandbox;
            _logger = logger;
        }

        public async Task GradeAsync(Guid submissionId, CancellationToken cancellationToken = default)
        {
            var submission = await _submissionRepository.GetByIdAsync(submissionId);
            if(submission == null)
            {
                _logger.LogWarning("Submission {id} not found.", submissionId);
                return;
            }

            var problem = await _problemRepository.GetByIdAsync(submission.ProblemId);
            if(problem == null)
            {
                _logger.LogWarning("Problem {Id} not found for submission {SubId}.", submission.ProblemId, submissionId);
                return;
            }

            var testCases = (await _testCaseRepository.GetByProblemIdAsync(submission.ProblemId)).ToList();
            if(!testCases.Any())
            {
                _logger.LogWarning("No test cases found for problem {Id}.", submission.ProblemId);
                return;
            }

            var workDir = Path.Combine(Path.GetTempPath(), "algojudge", submissionId.ToString());
            Directory.CreateDirectory(workDir);

            try
            {
                submission.Status = SubmissionStatus.Compiling;
                await _unitOfWork.SaveChangesAsync();

                var compileResult = await _sandbox.CompileAsync(submission.SourceCode, workDir, cancellationToken);
                if(!compileResult.Success)
                {
                    submission.Status = SubmissionStatus.CompileError;
                    await _unitOfWork.SaveChangesAsync();
                    _logger.LogInformation("Submission {Id} → CompileError.", submissionId);
                    return;
                }

                var finalStatus = SubmissionStatus.Accepted;
                int maxExecutionTime = 0;
                long maxMemoryUsed = 0;

                foreach(var testCase in testCases)
                {
                    var runResult = await _sandbox.RunAsync(workDir, testCase.Input, problem.TimeLimit, problem.MemoryLimit, cancellationToken);

                    if (runResult.ExecutionTimeMs > maxExecutionTime)
                        maxExecutionTime = runResult.ExecutionTimeMs;

                    if (runResult.MemoryUsedBytes > maxMemoryUsed)
                        maxMemoryUsed = runResult.MemoryUsedBytes;

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
                        _logger.LogError("SystemError on submission {Id}, testCase {TcId}.",
                            submissionId, testCase.Id);
                        finalStatus = SubmissionStatus.RuntimeError;
                        break;
                    }

                    var actualOutput = runResult.Output.Trim();
                    var expectedOutput = testCase.ExpectedOutput.Trim();

                    _logger.LogInformation("Actual  : [{Actual}]", actualOutput);
                    _logger.LogInformation("Expected: [{Expected}]", expectedOutput);

                    if (actualOutput != expectedOutput)
                    {
                        finalStatus = SubmissionStatus.WrongAnswer;
                        break;
                    }

                    long memoryLimitBytes = (long)problem.MemoryLimit * 1024;
                    if (runResult.MemoryUsedBytes > memoryLimitBytes)
                    {
                        finalStatus = SubmissionStatus.MemoryLimitExceeded;
                        break;
                    }
                }
                submission.Status = finalStatus;
                submission.ExecutionTime = maxExecutionTime;
                submission.MemoryUsed = (int)(maxMemoryUsed / 1024);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("Submission {Id} → {Status} | {Time}ms | {Mem}KB",
                    submissionId, finalStatus, maxExecutionTime, submission.MemoryUsed);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Unexpected error grading submission {Id}.", submissionId);
                submission.Status = SubmissionStatus.RuntimeError;
                await _unitOfWork.SaveChangesAsync();
            }
            finally
            {
                if (Directory.Exists(workDir))
                    Directory.Delete(workDir, recursive: true);
            }
        }
    }
}
