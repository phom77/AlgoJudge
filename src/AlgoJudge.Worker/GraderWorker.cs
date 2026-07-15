using AlgoJudge.Application.Interfaces;
using AlgoJudge.Application.Models.SubmissionQueue;

namespace AlgoJudge.Worker
{
    public class GraderWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly SubmissionQueueOptions _options;
        private readonly WorkerIdentity _identity;
        private readonly ILogger<GraderWorker> _logger;

        public GraderWorker(
            IServiceScopeFactory scopeFactory,
            SubmissionQueueOptions options,
            WorkerIdentity identity,
            ILogger<GraderWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _options = options;
            _identity = identity;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "Grader worker {WorkerId} started with a {LeaseSeconds}-second lease.",
                _identity.Value,
                _options.LeaseDurationSeconds);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var processedClaim = await TryProcessNextClaimAsync(stoppingToken);
                    if (!processedClaim)
                        await Task.Delay(_options.PollInterval, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Unexpected error in the grader worker loop.");
                    await Task.Delay(_options.PollInterval, stoppingToken);
                }
            }

            _logger.LogInformation("Grader worker {WorkerId} stopped.", _identity.Value);
        }

        private async Task<bool> TryProcessNextClaimAsync(CancellationToken stoppingToken)
        {
            SubmissionClaim? claim;
            using (var claimScope = _scopeFactory.CreateScope())
            {
                var repository = claimScope.ServiceProvider
                    .GetRequiredService<ISubmissionRepository>();
                claim = await repository.ClaimNextAsync(
                    _identity.Value,
                    _options.LeaseDuration,
                    _options.MaxAttempts,
                    stoppingToken);
            }

            if (claim is null)
                return false;

            _logger.LogInformation(
                "Worker {WorkerId} claimed submission {SubmissionId}, attempt {AttemptCount}.",
                _identity.Value,
                claim.SubmissionId,
                claim.AttemptCount);

            await ProcessClaimAsync(claim, stoppingToken);
            return true;
        }

        private async Task ProcessClaimAsync(
            SubmissionClaim claim,
            CancellationToken stoppingToken)
        {
            using var leaseLostSource = new CancellationTokenSource();
            using var gradingSource = CancellationTokenSource.CreateLinkedTokenSource(
                stoppingToken,
                leaseLostSource.Token);
            using var heartbeatSource = CancellationTokenSource.CreateLinkedTokenSource(
                stoppingToken);

            var heartbeatTask = MaintainLeaseAsync(
                claim,
                leaseLostSource,
                heartbeatSource.Token);
            var shouldAbandon = false;

            try
            {
                using var gradingScope = _scopeFactory.CreateScope();
                var grader = gradingScope.ServiceProvider.GetRequiredService<IGraderService>();
                await grader.GradeAsync(claim, gradingSource.Token);
            }
            catch (SubmissionClaimLostException)
            {
                _logger.LogWarning(
                    "Worker {WorkerId} lost ownership of submission {SubmissionId}.",
                    _identity.Value,
                    claim.SubmissionId);
            }
            catch (OperationCanceledException) when (leaseLostSource.IsCancellationRequested)
            {
                _logger.LogWarning(
                    "Grading submission {SubmissionId} stopped because its lease was lost.",
                    claim.SubmissionId);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                shouldAbandon = true;
            }
            catch (Exception exception)
            {
                shouldAbandon = true;
                _logger.LogError(
                    exception,
                    "Grading attempt {AttemptCount} failed for submission {SubmissionId}.",
                    claim.AttemptCount,
                    claim.SubmissionId);
            }
            finally
            {
                heartbeatSource.Cancel();
                try
                {
                    await heartbeatTask;
                }
                catch (OperationCanceledException)
                {
                    // Expected when grading finishes or the host stops.
                }
            }

            if (shouldAbandon && !leaseLostSource.IsCancellationRequested)
                await AbandonClaimAsync(claim);
        }

        private async Task MaintainLeaseAsync(
            SubmissionClaim claim,
            CancellationTokenSource leaseLostSource,
            CancellationToken cancellationToken)
        {
            try
            {
                using var timer = new PeriodicTimer(_options.HeartbeatInterval);
                while (await timer.WaitForNextTickAsync(cancellationToken))
                {
                    using var scope = _scopeFactory.CreateScope();
                    var repository = scope.ServiceProvider
                        .GetRequiredService<ISubmissionRepository>();
                    var renewed = await repository.RenewLeaseAsync(
                        claim,
                        _options.LeaseDuration,
                        cancellationToken);
                    if (renewed)
                        continue;

                    leaseLostSource.Cancel();
                    return;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Lease heartbeat failed for submission {SubmissionId}.",
                    claim.SubmissionId);
                leaseLostSource.Cancel();
            }
        }

        private async Task AbandonClaimAsync(SubmissionClaim claim)
        {
            using var timeoutSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var repository = scope.ServiceProvider
                    .GetRequiredService<ISubmissionRepository>();
                var abandoned = await repository.AbandonClaimAsync(
                    claim,
                    _options.MaxAttempts,
                    timeoutSource.Token);

                if (!abandoned)
                {
                    _logger.LogWarning(
                        "Could not release submission {SubmissionId}; its claim is already stale.",
                        claim.SubmissionId);
                }
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Could not release submission {SubmissionId}; lease recovery will handle it.",
                    claim.SubmissionId);
            }
        }
    }
}
