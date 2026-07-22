using AlgoJudge.Application.Contracts.Admin;

namespace AlgoJudge.Application.Interfaces;

public interface IProblemAuthoringService
{
    Task<ProblemDraftResponse> CreateDraftAsync(Guid ownerUserId, CreateProblemDraftRequest request, CancellationToken cancellationToken = default);
    Task<ProblemDraftResponse> CreateNextRevisionAsync(Guid ownerUserId, int problemId, CancellationToken cancellationToken = default);
    Task<ProblemDraftResponse> GetDraftAsync(Guid ownerUserId, Guid revisionId, CancellationToken cancellationToken = default);
    Task<ProblemDraftResponse> UpdateMetadataAsync(Guid ownerUserId, Guid revisionId, UpdateProblemDraftRequest request, CancellationToken cancellationToken = default);
    Task<ProblemDraftResponse> UpdateSignatureAsync(Guid ownerUserId, Guid revisionId, UpdateFunctionSignatureRequest request, CancellationToken cancellationToken = default);
    Task<ProblemDraftResponse> UpdateHandwrittenCasesAsync(Guid ownerUserId, Guid revisionId, UpdateHandwrittenCasesRequest request, CancellationToken cancellationToken = default);
    Task<ProblemDraftResponse> UpdateSourcesAsync(Guid ownerUserId, Guid revisionId, UpdateAuthoringSourcesRequest request, CancellationToken cancellationToken = default);
    Task<ContentGenerationStatusResponse> StartGenerationAsync(Guid ownerUserId, Guid revisionId, CancellationToken cancellationToken = default);
    Task<ContentGenerationStatusResponse> GetGenerationStatusAsync(Guid ownerUserId, Guid revisionId, CancellationToken cancellationToken = default);
    Task<GeneratedSuiteReviewResponse> GetSuiteReviewAsync(Guid ownerUserId, Guid revisionId, CancellationToken cancellationToken = default);
    Task PublishAsync(Guid ownerUserId, Guid revisionId, CancellationToken cancellationToken = default);
}
