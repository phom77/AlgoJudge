using AlgoJudge.Application.DTOs.Common;
using AlgoJudge.Application.DTOs.Problem;

namespace AlgoJudge.Application.Interfaces
{
    public interface IProblemService
    {
        Task<ProblemDto?> GetProblemByIdAsync(int id);
        Task<PagedResult<ProblemDto>> GetProblemAsync(int pageNumber, int pageSize);
    }
}
