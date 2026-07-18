namespace AlgoJudge.Application.Models.RunQueue;

public sealed class RunClaimLostException : InvalidOperationException
{
    public RunClaimLostException(Guid runId)
        : base($"The claim for run {runId} is no longer owned by this worker.")
    {
    }
}
