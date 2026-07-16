using AlgoJudge.Application.Contracts.Common;
using AlgoJudge.Application.Contracts.Submissions;

namespace AlgoJudge.Application.Interfaces;

public interface ISubmissionService
{
    Task<SubmissionResponse> SubmitCodeAsync(
        CreateSubmissionRequest request,
        Guid userId);
    Task<SubmissionResponse?> GetSubmissionByIdAsync(Guid id, Guid requesterId);
    Task<PagedResponse<SubmissionResponse>> GetHistoryAsync(
        Guid userId,
        SubmissionHistoryQuery query);
}
