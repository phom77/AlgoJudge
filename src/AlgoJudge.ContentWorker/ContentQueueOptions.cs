namespace AlgoJudge.ContentWorker;

public sealed class ContentQueueOptions
{
    public int PollIntervalSeconds { get; set; } = 3;
    public int LeaseDurationSeconds { get; set; } = 120;
    public int HeartbeatIntervalSeconds { get; set; } = 30;
    public int MaxAttempts { get; set; } = 3;
    public string? WorkerId { get; set; }
    public TimeSpan PollInterval => TimeSpan.FromSeconds(PollIntervalSeconds);
    public TimeSpan LeaseDuration => TimeSpan.FromSeconds(LeaseDurationSeconds);
    public TimeSpan HeartbeatInterval => TimeSpan.FromSeconds(HeartbeatIntervalSeconds);
    public void Validate()
    {
        if (PollIntervalSeconds is < 1 or > 60 || LeaseDurationSeconds is < 30 or > 3600 ||
            HeartbeatIntervalSeconds < 1 || HeartbeatIntervalSeconds >= LeaseDurationSeconds ||
            MaxAttempts is < 1 or > 10 || WorkerId?.Length > 128)
            throw new InvalidOperationException("ContentQueue configuration is invalid.");
    }
}
