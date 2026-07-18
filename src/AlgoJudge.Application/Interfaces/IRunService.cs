using AlgoJudge.Application.Contracts.Runs;

namespace AlgoJudge.Application.Interfaces;

public interface IRunService
{
    Task<RunResponse> CreateAsync(
        string problemSlug,
        CreateRunRequest request,
        Guid userId,
        CancellationToken cancellationToken = default);
    Task<RunResponse?> GetByIdAsync(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken = default);
}
