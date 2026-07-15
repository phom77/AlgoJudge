using AlgoJudge.Application.Contracts.Common;
using AlgoJudge.Application.Contracts.Problems;

namespace AlgoJudge.Application.Interfaces
{
    public interface IProblemService
    {
        Task<PagedResponse<ProblemListItemResponse>> GetProblemsAsync(
            ProblemListQuery query,
            Guid? userId);

        Task<ProblemDetailResponse?> GetProblemBySlugAsync(string slug, Guid? userId);
    }
}
