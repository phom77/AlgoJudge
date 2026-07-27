using AlgoJudge.Application.Contracts.Admin;
using AlgoJudge.Application.Contracts.Common;

namespace AlgoJudge.Application.Interfaces;

public interface IProblemManagementService
{
    Task<PagedResponse<AdminProblemListItemResponse>> GetProblemsAsync(
        AdminProblemListQuery query,
        CancellationToken cancellationToken = default);
    Task<AdminProblemResponse> GetProblemAsync(
        int problemId,
        CancellationToken cancellationToken = default);
    Task ArchiveAsync(int problemId, CancellationToken cancellationToken = default);
    Task RestoreAsync(int problemId, CancellationToken cancellationToken = default);
}
