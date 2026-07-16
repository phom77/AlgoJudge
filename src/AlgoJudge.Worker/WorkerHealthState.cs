using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AlgoJudge.Worker;

public sealed class WorkerHealthState
{
    private volatile bool _isRunning;

    public void MarkRunning() => _isRunning = true;

    public void MarkStopped() => _isRunning = false;

    public HealthCheckResult Check()
    {
        return _isRunning
            ? HealthCheckResult.Healthy("The grader background service is running.")
            : HealthCheckResult.Unhealthy("The grader background service is not running.");
    }
}

public sealed class WorkerProcessHealthCheck(WorkerHealthState state) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(state.Check());
    }
}
