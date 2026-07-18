using AlgoJudge.Application.Models.Execution;

namespace AlgoJudge.Application.Interfaces;

public interface ITestSuiteProvider
{
    Task<SystemTestSuite?> GetSystemSuiteAsync(
        int problemId,
        int version,
        CancellationToken cancellationToken = default);
}
