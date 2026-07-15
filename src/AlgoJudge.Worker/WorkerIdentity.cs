namespace AlgoJudge.Worker;

public sealed record WorkerIdentity(string Value)
{
    public static WorkerIdentity Create(string? configuredWorkerId)
    {
        var value = string.IsNullOrWhiteSpace(configuredWorkerId)
            ? $"{Environment.MachineName}-{Environment.ProcessId}-{Guid.NewGuid():N}"
            : configuredWorkerId.Trim();

        if (value.Length > 128)
            throw new InvalidOperationException("The resolved worker ID exceeds 128 characters.");

        return new WorkerIdentity(value);
    }
}
