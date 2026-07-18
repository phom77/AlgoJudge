using AlgoJudge.Application.Interfaces;
using AlgoJudge.Application.Models.RunQueue;
using AlgoJudge.Application.Models.SubmissionQueue;
using AlgoJudge.Domain.Enums;

namespace AlgoJudge.Worker;

public sealed class GraderWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SubmissionQueueOptions _options;
    private readonly WorkerIdentity _identity;
    private readonly WorkerHealthState _healthState;
    private readonly ILogger<GraderWorker> _logger;
    private bool _preferRuns;

    public GraderWorker(IServiceScopeFactory scopeFactory, SubmissionQueueOptions options,
        WorkerIdentity identity, WorkerHealthState healthState, ILogger<GraderWorker> logger)
    {
        _scopeFactory = scopeFactory; _options = options; _identity = identity;
        _healthState = healthState; _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Grader worker {WorkerId} started with a {LeaseSeconds}-second lease.", _identity.Value, _options.LeaseDurationSeconds);
        _healthState.MarkRunning();
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (!await TryProcessNextClaimAsync(stoppingToken))
                        await Task.Delay(_options.PollInterval, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Unexpected error in the grader worker loop.");
                    await Task.Delay(_options.PollInterval, stoppingToken);
                }
            }
        }
        finally
        {
            _healthState.MarkStopped();
            _logger.LogInformation("Grader worker {WorkerId} stopped.", _identity.Value);
        }
    }

    private async Task<bool> TryProcessNextClaimAsync(CancellationToken cancellationToken)
    {
        ClaimedExecution? execution;
        using (var scope = _scopeFactory.CreateScope())
        {
            execution = _preferRuns
                ? await ClaimRunThenSubmissionAsync(scope.ServiceProvider, cancellationToken)
                : await ClaimSubmissionThenRunAsync(scope.ServiceProvider, cancellationToken);
        }
        if (execution is null) return false;
        _preferRuns = execution.Kind == ExecutionKind.Submit;
        _logger.LogInformation("Worker {WorkerId} claimed {ExecutionKind} {ExecutionId}, attempt {AttemptCount}.",
            _identity.Value, execution.Kind, execution.Id, execution.AttemptCount);
        await ProcessClaimAsync(execution, cancellationToken);
        return true;
    }

    private async Task<ClaimedExecution?> ClaimSubmissionThenRunAsync(IServiceProvider services, CancellationToken token)
    {
        var submission = await services.GetRequiredService<ISubmissionRepository>()
            .ClaimNextAsync(_identity.Value, _options.LeaseDuration, _options.MaxAttempts, token);
        if (submission is not null) return ClaimedExecution.From(submission);
        var run = await services.GetRequiredService<IRunRepository>()
            .ClaimNextAsync(_identity.Value, _options.LeaseDuration, _options.MaxAttempts, token);
        return run is null ? null : ClaimedExecution.From(run);
    }

    private async Task<ClaimedExecution?> ClaimRunThenSubmissionAsync(IServiceProvider services, CancellationToken token)
    {
        var run = await services.GetRequiredService<IRunRepository>()
            .ClaimNextAsync(_identity.Value, _options.LeaseDuration, _options.MaxAttempts, token);
        if (run is not null) return ClaimedExecution.From(run);
        var submission = await services.GetRequiredService<ISubmissionRepository>()
            .ClaimNextAsync(_identity.Value, _options.LeaseDuration, _options.MaxAttempts, token);
        return submission is null ? null : ClaimedExecution.From(submission);
    }

    private async Task ProcessClaimAsync(ClaimedExecution execution, CancellationToken stoppingToken)
    {
        using var leaseLost = new CancellationTokenSource();
        using var work = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, leaseLost.Token);
        using var heartbeat = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var heartbeatTask = MaintainLeaseAsync(execution, leaseLost, heartbeat.Token);
        var shouldAbandon = false;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            if (execution.Submission is { } submission)
                await scope.ServiceProvider.GetRequiredService<IGraderService>().GradeAsync(submission, work.Token);
            else
                await scope.ServiceProvider.GetRequiredService<IRunGraderService>().GradeAsync(execution.Run!, work.Token);
        }
        catch (Exception exception) when (exception is SubmissionClaimLostException or RunClaimLostException)
        {
            _logger.LogWarning("Worker {WorkerId} lost ownership of {ExecutionKind} {ExecutionId}.", _identity.Value, execution.Kind, execution.Id);
        }
        catch (OperationCanceledException) when (leaseLost.IsCancellationRequested)
        {
            _logger.LogWarning("Grading {ExecutionKind} {ExecutionId} stopped because its lease was lost.", execution.Kind, execution.Id);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { shouldAbandon = true; }
        catch (Exception exception)
        {
            shouldAbandon = true;
            _logger.LogError(exception, "Grading attempt {AttemptCount} failed for {ExecutionKind} {ExecutionId}.", execution.AttemptCount, execution.Kind, execution.Id);
        }
        finally
        {
            heartbeat.Cancel();
            try { await heartbeatTask; } catch (OperationCanceledException) { }
        }
        if (shouldAbandon && !leaseLost.IsCancellationRequested) await AbandonAsync(execution);
    }

    private async Task MaintainLeaseAsync(ClaimedExecution execution, CancellationTokenSource leaseLost, CancellationToken token)
    {
        try
        {
            using var timer = new PeriodicTimer(_options.HeartbeatInterval);
            while (await timer.WaitForNextTickAsync(token))
            {
                using var scope = _scopeFactory.CreateScope();
                var renewed = execution.Submission is { } submission
                    ? await scope.ServiceProvider.GetRequiredService<ISubmissionRepository>().RenewLeaseAsync(submission, _options.LeaseDuration, token)
                    : await scope.ServiceProvider.GetRequiredService<IRunRepository>().RenewLeaseAsync(execution.Run!, _options.LeaseDuration, token);
                if (!renewed) { leaseLost.Cancel(); return; }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Lease heartbeat failed for {ExecutionKind} {ExecutionId}.", execution.Kind, execution.Id);
            leaseLost.Cancel();
        }
    }

    private async Task AbandonAsync(ClaimedExecution execution)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var abandoned = execution.Submission is { } submission
                ? await scope.ServiceProvider.GetRequiredService<ISubmissionRepository>().AbandonClaimAsync(submission, _options.MaxAttempts, timeout.Token)
                : await scope.ServiceProvider.GetRequiredService<IRunRepository>().AbandonClaimAsync(execution.Run!, _options.MaxAttempts, timeout.Token);
            if (!abandoned) _logger.LogWarning("Could not release {ExecutionKind} {ExecutionId}; its claim is already stale.", execution.Kind, execution.Id);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not release {ExecutionKind} {ExecutionId}; lease recovery will handle it.", execution.Kind, execution.Id);
        }
    }

    private sealed record ClaimedExecution(ExecutionKind Kind, SubmissionClaim? Submission, RunClaim? Run)
    {
        public Guid Id => Submission?.SubmissionId ?? Run!.RunId;
        public int AttemptCount => Submission?.AttemptCount ?? Run!.AttemptCount;
        public static ClaimedExecution From(SubmissionClaim claim) => new(ExecutionKind.Submit, claim, null);
        public static ClaimedExecution From(RunClaim claim) => new(ExecutionKind.Run, null, claim);
    }
}
