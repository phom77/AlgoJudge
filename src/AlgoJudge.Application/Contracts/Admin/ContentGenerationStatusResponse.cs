using AlgoJudge.Domain.Enums;

namespace AlgoJudge.Application.Contracts.Admin;

public sealed class ContentGenerationStatusResponse
{
    public Guid JobId { get; init; }
    public Guid RevisionId { get; init; }
    public ContentGenerationJobStatus JobStatus { get; init; }
    public AuthoringRevisionStatus RevisionStatus { get; init; }
    public int AttemptCount { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? FinishedAt { get; init; }
}
