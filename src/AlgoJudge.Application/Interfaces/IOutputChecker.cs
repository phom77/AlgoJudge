using AlgoJudge.Domain.Execution;

namespace AlgoJudge.Application.Interfaces;

public interface IOutputChecker
{
    bool IsMatch(
        OutputCheckerConfiguration configuration,
        string expectedOutput,
        string actualOutput);
}
