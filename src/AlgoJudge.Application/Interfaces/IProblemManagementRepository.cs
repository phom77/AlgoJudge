using AlgoJudge.Application.Models.Admin;
using AlgoJudge.Application.Models.Common;
using AlgoJudge.Domain.Enums;

namespace AlgoJudge.Application.Interfaces;

public interface IProblemManagementRepository
{
    Task<PagedResult<AdminProblemDto>> GetPagedAsync(
        string? search,
        ProblemStatus? status,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<AdminProblemDto?> GetAsync(int problemId, CancellationToken cancellationToken = default);
    Task<bool> TransitionStatusAsync(
        int problemId,
        ProblemStatus expectedStatus,
        ProblemStatus nextStatus,
        CancellationToken cancellationToken = default);
}
