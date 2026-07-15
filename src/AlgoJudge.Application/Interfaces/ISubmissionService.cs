using AlgoJudge.Application.DTOs.Common;
using AlgoJudge.Application.DTOs.Submission;
using AlgoJudge.Domain.Enums;

namespace AlgoJudge.Application.Interfaces
{
    public interface ISubmissionService
    {
        Task<SubmissionDto> SubmitCodeAsync(CreateSubmissionDto dto, Guid userId);
        Task<SubmissionDto?> GetSubmissionByIdAsync(Guid id, Guid requesterId);
        Task<PagedResult<SubmissionDto>> GetHistoryAsync(
            Guid userId,
            int? problemId,
            SubmissionStatus? status,
            int pageNumber,
            int pageSize);
    }
}
