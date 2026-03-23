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
        private readonly ILogger<GraderService> _logger;

        public GraderService(
            ISubmissionRepository submissionRepository,
            IProblemRepository problemRepository,
            ITestCaseRepository testCaseRepository,
            IUnitOfWork unitOfWork,
            ILogger<GraderService> logger)
        {
            _submissionRepository = submissionRepository;
            _problemRepository = problemRepository;
            _testCaseRepository = testCaseRepository;
            _unitOfWork = unitOfWork;
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

            var sourceFile = Path.Combine(workDir, "solution.cpp");
            var executableFile = Path.Combine(workDir,
                OperatingSystem.IsWindows() ? "solution.exe" : "solution");

            try
            {
                submission.Status = SubmissionStatus.Compiling;
                await _unitOfWork.SaveChangesAsync();

                await File.WriteAllTextAsync(sourceFile, submission.SourceCode, cancellationToken);

                var compileResult = await CompileAsync(sourceFile, executableFile, cancellationToken);
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
                    var runResult = await RunAsync(executableFile, testCase.Input, problem.TimeLimit, cancellationToken);

                    if (runResult.ExecutionTimeMs > maxExecutionTime)
                        maxExecutionTime = runResult.ExecutionTimeMs;

                    if (runResult.MemoryUsedBytes > maxMemoryUsed)
                        maxMemoryUsed = runResult.MemoryUsedBytes;

                    if(runResult.Status != SubmissionStatus.Accepted)
                    {
                        finalStatus = runResult.Status;
                        break;
                    }

                    var actualOutput = runResult.Output.Trim();
                    var expectedOutput = testCase.ExpectedOutput.Trim();

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

        private async Task<(bool Success, string ErrorOutput)> CompileAsync(
            string sourceFile, string outputFile, CancellationToken ct)
        {
            var isWindows = OperatingSystem.IsWindows();
            var compiler = isWindows ? "g++" : "g++";
            var args = OperatingSystem.IsWindows()
                ? $"\"{sourceFile}\" -o \"{outputFile}\" -O2 -std=c++17"
                : $"\"{sourceFile}\" -o \"{outputFile}\" -O2 -std=c++17 -lm";

            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = compiler,
                Arguments = args,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            process.Start();
            var errorOutput = await process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            return (process.ExitCode == 0, errorOutput);
        }

        private async Task<RunResult> RunAsync(
            string executableFile, string input, int timeLimitMs, CancellationToken ct)
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = executableFile,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            process.Start();

            await process.StandardInput.WriteAsync(input);
            process.StandardInput.Close();

            var stopwatch = Stopwatch.StartNew();

            var finished = process.WaitForExit(timeLimitMs);
            stopwatch.Stop();

            if (!finished)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                return new RunResult
                {
                    Status = SubmissionStatus.TimeLimitExceeded,
                    ExecutionTimeMs = timeLimitMs,
                    MemoryUsedBytes = 0,
                    Output = string.Empty
                };
            }

            var output = await process.StandardOutput.ReadToEndAsync(ct);
            var stderr = await process.StandardError.ReadToEndAsync(ct);
            var elapsedMs = (int)stopwatch.ElapsedMilliseconds;

            long peakMemoryBytes = 0;
            try { peakMemoryBytes = process.PeakWorkingSet64; } catch { }

            if (process.ExitCode != 0)
            {
                _logger.LogWarning("RuntimeError — exit code: {Code}, stderr: {Err}",
                    process.ExitCode, stderr);
                return new RunResult
                {
                    Status = SubmissionStatus.RuntimeError,
                    ExecutionTimeMs = elapsedMs,
                    MemoryUsedBytes = peakMemoryBytes,
                    Output = string.Empty
                };
            }

            return new RunResult
            {
                Status = SubmissionStatus.Accepted,
                ExecutionTimeMs = elapsedMs,
                MemoryUsedBytes = peakMemoryBytes,
                Output = output
            };
        }
    }

    internal class RunResult
    {
        public SubmissionStatus Status { get; set; }
        public int ExecutionTimeMs { get; set; }
        public long MemoryUsedBytes { get; set; }
        public string Output { get; set; } = string.Empty;
    }
}
