namespace AlgoJudge.Worker;

public sealed class SubmissionQueueOptions
{
    public int PollIntervalSeconds { get; set; } = 3;
    public int LeaseDurationSeconds { get; set; } = 60;
    public int HeartbeatIntervalSeconds { get; set; } = 15;
    public int MaxAttempts { get; set; } = 3;
    public string? WorkerId { get; set; }

    public TimeSpan PollInterval => TimeSpan.FromSeconds(PollIntervalSeconds);
    public TimeSpan LeaseDuration => TimeSpan.FromSeconds(LeaseDurationSeconds);
    public TimeSpan HeartbeatInterval => TimeSpan.FromSeconds(HeartbeatIntervalSeconds);

    public void Validate()
    {
        if (PollIntervalSeconds is < 1 or > 60)
            throw new InvalidOperationException("Queue:PollIntervalSeconds must be between 1 and 60.");

        if (LeaseDurationSeconds is < 10 or > 3600)
            throw new InvalidOperationException("Queue:LeaseDurationSeconds must be between 10 and 3600.");

        if (HeartbeatIntervalSeconds < 1 || HeartbeatIntervalSeconds >= LeaseDurationSeconds)
        {
            throw new InvalidOperationException(
                "Queue:HeartbeatIntervalSeconds must be positive and shorter than the lease.");
        }

        if (MaxAttempts is < 1 or > 10)
            throw new InvalidOperationException("Queue:MaxAttempts must be between 1 and 10.");

        if (WorkerId?.Length > 128)
            throw new InvalidOperationException("Queue:WorkerId cannot exceed 128 characters.");
    }
}
