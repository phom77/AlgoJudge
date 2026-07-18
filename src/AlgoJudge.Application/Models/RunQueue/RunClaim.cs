namespace AlgoJudge.Application.Models.RunQueue;

public sealed record RunClaim(
    Guid RunId,
    Guid ClaimToken,
    string WorkerId,
    int AttemptCount,
    DateTime LeaseExpiresAt);
