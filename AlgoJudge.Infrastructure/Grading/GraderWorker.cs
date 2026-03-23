using AlgoJudge.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace AlgoJudge.Infrastructure.Grading
{
    public class GraderWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<GraderWorker> _logger;

        public GraderWorker(IServiceScopeFactory scopeFactory, ILogger<GraderWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("GraderWorker started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessPendingSubmissionsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in GraderWorker loop.");
                }

                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }

            _logger.LogInformation("GraderWorker stopped.");
        }

        private async Task ProcessPendingSubmissionsAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();

            var submissionRepo = scope.ServiceProvider.GetRequiredService<ISubmissionRepository>();
            var graderService = scope.ServiceProvider.GetRequiredService<IGraderService>();

            var pendingSubmissions = await submissionRepo.GetPendingAsync();
            if (!pendingSubmissions.Any()) return;

            _logger.LogInformation("Found {Count} pending submission(s).", pendingSubmissions.Count());

            foreach (var submission in pendingSubmissions)
            {
                if (ct.IsCancellationRequested) break;

                _logger.LogInformation("Grading submission {Id}...", submission.Id);
                await graderService.GradeAsync(submission.Id, ct);
            }
        }
    }
}
