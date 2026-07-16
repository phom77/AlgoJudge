using AlgoJudge.Application.Models.Common;
using AlgoJudge.Domain.Entities;
using AlgoJudge.Domain.Enums;

namespace AlgoJudge.Application.Interfaces
{
    public interface IProblemRepository
    {
        Task<Problem?> GetByIdAsync(int id);
        Task<Problem?> GetPublishedBySlugAsync(string slug);
        Task<PagedResult<Problem>> GetPublishedPagedAsync(
            string? search,
            DifficultyLevel? difficulty,
            IReadOnlyCollection<string> tags,
            Guid? userId,
            bool? solved,
            int pageNumber,
            int pageSize);
    }
}
