using AlgoJudge.Domain.Entities;

namespace AlgoJudge.Application.Interfaces
{
    public interface IJudgeTestCaseRepository
    {
        Task<IEnumerable<JudgeTestCase>> GetByProblemIdAsync(int problemId);
    }
}
