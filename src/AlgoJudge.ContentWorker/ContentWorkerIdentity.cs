namespace AlgoJudge.ContentWorker;

public sealed record ContentWorkerIdentity(string Value)
{
    public static ContentWorkerIdentity Create(string? configured) => new(
        string.IsNullOrWhiteSpace(configured)
            ? $"content-{Environment.MachineName}-{Environment.ProcessId}"
            : configured.Trim());
}
