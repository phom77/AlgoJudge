using AlgoJudge.Application.DTOs.Common;
using AlgoJudge.Domain.Entities;
using AlgoJudge.Domain.Enums;

namespace AlgoJudge.Application.Interfaces
{
    public interface ISubmissionRepository
    {
        Task AddAsync(Submission submission);
        Task<Submission?> GetByIdAsync(Guid id);
        Task<IEnumerable<Submission>> GetPendingAsync();
        Task<PagedResult<Submission>> GetPagedAsync(
            Guid userId,
            int? problemId,
            SubmissionStatus? status,
            int pageNumber,
            int pageSize);
    }
}
