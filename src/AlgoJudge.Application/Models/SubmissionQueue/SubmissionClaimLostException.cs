namespace AlgoJudge.Application.Models.SubmissionQueue;

public sealed class SubmissionClaimLostException : Exception
{
    public SubmissionClaimLostException(Guid submissionId)
        : base($"Submission claim {submissionId} is no longer owned by this worker.")
    {
        SubmissionId = submissionId;
    }

    public Guid SubmissionId { get; }
}
