using AlgoJudge.Application.DTOs.Common;
using AlgoJudge.Domain.Entities;

namespace AlgoJudge.Application.Interfaces
{
    public interface IProblemRepository
    {
        Task<Problem?> GetByIdAsync(int id);
        Task<PagedResult<Problem>> GetPagedAsync(int pageNumber, int pageSize);
    }
}
