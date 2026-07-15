namespace AlgoJudge.Application.Models.SubmissionQueue;

public sealed record SubmissionClaim(
    Guid SubmissionId,
    Guid ClaimToken,
    string WorkerId,
    int AttemptCount,
    DateTime LeaseExpiresAt);
