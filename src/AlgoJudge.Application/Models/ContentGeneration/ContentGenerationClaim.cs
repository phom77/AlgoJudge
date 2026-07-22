namespace AlgoJudge.Application.Models.ContentGeneration;

public sealed record ContentGenerationClaim(
    Guid JobId,
    Guid RevisionId,
    Guid ClaimToken,
    string WorkerId,
    int AttemptCount,
    DateTime LeaseExpiresAt,
    string DefinitionSnapshotJson,
    string DefinitionSha256,
    int TimeLimitMs,
    int MemoryLimitKb);
