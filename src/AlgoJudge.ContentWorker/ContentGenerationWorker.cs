using AlgoJudge.Application.Interfaces;
using AlgoJudge.Application.Models.ContentGeneration;

namespace AlgoJudge.ContentWorker;

public sealed class ContentGenerationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ContentQueueOptions _options;
    private readonly ContentWorkerIdentity _identity;
    private readonly ILogger<ContentGenerationWorker> _logger;

    public ContentGenerationWorker(IServiceScopeFactory scopeFactory, ContentQueueOptions options,
        ContentWorkerIdentity identity, ILogger<ContentGenerationWorker> logger)
    {
        _scopeFactory = scopeFactory; _options = options; _identity = identity; _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Content worker {WorkerId} started.", _identity.Value);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                ContentGenerationClaim? claim;
                using (var scope = _scopeFactory.CreateScope())
                    claim = await scope.ServiceProvider.GetRequiredService<IContentGenerationJobRepository>()
                        .ClaimNextAsync(_identity.Value, _options.LeaseDuration, _options.MaxAttempts, stoppingToken);
                if (claim is null) { await Task.Delay(_options.PollInterval, stoppingToken); continue; }
                _logger.LogInformation("Content worker {WorkerId} claimed generation job {JobId}, attempt {AttemptCount}.",
                    _identity.Value, claim.JobId, claim.AttemptCount);
                await ProcessAsync(claim, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Unexpected error in the content worker loop.");
                await Task.Delay(_options.PollInterval, stoppingToken);
            }
        }
        _logger.LogInformation("Content worker {WorkerId} stopped.", _identity.Value);
    }

    private async Task ProcessAsync(ContentGenerationClaim claim, CancellationToken stoppingToken)
    {
        using var leaseLost = new CancellationTokenSource();
        using var work = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, leaseLost.Token);
        using var heartbeat = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var heartbeatTask = MaintainLeaseAsync(claim, leaseLost, heartbeat.Token);
        var abandon = false;
        try
        {
            ContentGenerationResult result;
            using (var scope = _scopeFactory.CreateScope())
                result = await scope.ServiceProvider.GetRequiredService<IContentGenerationEngine>().GenerateAsync(claim, work.Token);
            using var completionScope = _scopeFactory.CreateScope();
            var completed = await completionScope.ServiceProvider.GetRequiredService<IContentGenerationJobRepository>()
                .CompleteAsync(claim, result, work.Token);
            if (!completed) _logger.LogWarning("Generation job {JobId} lost its claim before completion.", claim.JobId);
            else _logger.LogInformation("Generation job {JobId} produced {CaseCount} private cases.", claim.JobId, result.Cases.Count);
        }
        catch (ContentGenerationException exception) when (!leaseLost.IsCancellationRequested)
        {
            using var failureScope = _scopeFactory.CreateScope();
            var failed = await failureScope.ServiceProvider.GetRequiredService<IContentGenerationJobRepository>()
                .FailAsync(claim, exception.ErrorCode, exception.SafeMessage, stoppingToken);
            if (!failed) _logger.LogWarning("Generation job {JobId} lost its claim before failure was recorded.", claim.JobId);
            else _logger.LogWarning("Generation job {JobId} failed with safe category {ErrorCode}.", claim.JobId, exception.ErrorCode);
        }
        catch (OperationCanceledException) when (leaseLost.IsCancellationRequested)
        { _logger.LogWarning("Generation job {JobId} stopped because its lease was lost.", claim.JobId); }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { abandon = true; }
        catch (Exception exception)
        {
            abandon = true;
            _logger.LogError(
                "Generation attempt failed for job {JobId} with exception type {ExceptionType}; " +
                "private exception details were omitted.",
                claim.JobId,
                exception.GetType().Name);
        }
        finally
        {
            heartbeat.Cancel();
            try { await heartbeatTask; } catch (OperationCanceledException) { }
        }
        if (abandon && !leaseLost.IsCancellationRequested)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var scope = _scopeFactory.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IContentGenerationJobRepository>()
                .AbandonAsync(claim, _options.MaxAttempts, timeout.Token);
        }
    }

    private async Task MaintainLeaseAsync(ContentGenerationClaim claim, CancellationTokenSource lost, CancellationToken token)
    {
        using var timer = new PeriodicTimer(_options.HeartbeatInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(token))
            {
                using var scope = _scopeFactory.CreateScope();
                if (!await scope.ServiceProvider.GetRequiredService<IContentGenerationJobRepository>()
                    .RenewLeaseAsync(claim, _options.LeaseDuration, token)) { lost.Cancel(); return; }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Lease heartbeat failed for generation job {JobId}.", claim.JobId);
            lost.Cancel();
        }
    }
}
