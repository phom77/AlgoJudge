using AlgoJudge.Application.Interfaces;
using AlgoJudge.Application.Models.RunQueue;
using AlgoJudge.Domain.Entities;
using AlgoJudge.Domain.Enums;
using AlgoJudge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AlgoJudge.Infrastructure.Repositories;

public sealed class RunRepository : IRunRepository
{
    private readonly AppDbContext _context;
    public RunRepository(AppDbContext context) => _context = context;

    public async Task AddAsync(CodeRun run, CancellationToken cancellationToken = default) =>
        await _context.CodeRuns.AddAsync(run, cancellationToken);

    public Task<CodeRun?> GetByIdForUserAsync(Guid id, Guid userId, CancellationToken cancellationToken = default) =>
        _context.CodeRuns.AsNoTracking().SingleOrDefaultAsync(run => run.Id == id && run.UserId == userId, cancellationToken);

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.CodeRuns.AsNoTracking().AnyAsync(run => run.Id == id, cancellationToken);

    public Task<CodeRun?> GetClaimedAsync(RunClaim claim, CancellationToken cancellationToken = default) =>
        _context.CodeRuns.AsNoTracking().SingleOrDefaultAsync(run =>
            run.Id == claim.RunId && run.Status == RunStatus.Running &&
            run.WorkerId == claim.WorkerId && run.ClaimToken == claim.ClaimToken, cancellationToken);

    public async Task<RunClaim?> ClaimNextAsync(string workerId, TimeSpan leaseDuration, int maxAttempts, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);
        if (workerId.Length > 128) throw new ArgumentException("Worker ID cannot exceed 128 characters.", nameof(workerId));
        if (leaseDuration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(leaseDuration));
        if (maxAttempts <= 0) throw new ArgumentOutOfRangeException(nameof(maxAttempts));
        var token = Guid.NewGuid();
        var rows = await _context.Database.SqlQueryRaw<RunClaimRow>(ClaimNextSql,
            new NpgsqlParameter("pendingStatus", (object)(int)RunStatus.Pending),
            new NpgsqlParameter("runningStatus", (int)RunStatus.Running),
            new NpgsqlParameter("runtimeErrorStatus", (int)RunStatus.RuntimeError),
            new NpgsqlParameter("workerId", (object)workerId), new NpgsqlParameter("claimToken", (object)token),
            new NpgsqlParameter("leaseDuration", leaseDuration), new NpgsqlParameter("maxAttempts", maxAttempts))
            .ToListAsync(cancellationToken);
        var row = rows.SingleOrDefault();
        return row is null ? null : new RunClaim(row.RunId, row.ClaimToken, workerId, row.AttemptCount, row.LeaseExpiresAt);
    }

    public async Task<bool> RenewLeaseAsync(RunClaim claim, TimeSpan leaseDuration, CancellationToken cancellationToken = default)
    {
        if (leaseDuration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(leaseDuration));
        var rows = await _context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "CodeRuns" SET "LeaseExpiresAt" = CURRENT_TIMESTAMP + {leaseDuration}
            WHERE "Id" = {claim.RunId} AND "Status" = {(int)RunStatus.Running}
              AND "WorkerId" = {claim.WorkerId} AND "ClaimToken" = {claim.ClaimToken}
              AND "LeaseExpiresAt" > CURRENT_TIMESTAMP
            """, cancellationToken);
        return rows == 1;
    }

    public async Task<bool> FinalizeClaimAsync(RunClaim claim, RunStatus finalStatus, string? standardOutput, string? errorOutput, int executionTimeMs, int memoryUsedKb, CancellationToken cancellationToken = default)
    {
        if (finalStatus is RunStatus.Pending or RunStatus.Running) throw new ArgumentException("A claim requires a final status.", nameof(finalStatus));
        if (executionTimeMs < 0) throw new ArgumentOutOfRangeException(nameof(executionTimeMs));
        if (memoryUsedKb < 0) throw new ArgumentOutOfRangeException(nameof(memoryUsedKb));
        var rows = await _context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "CodeRuns" SET "Status" = {(int)finalStatus}, "StandardOutput" = {standardOutput},
              "ErrorOutput" = {errorOutput}, "ExecutionTimeMs" = {executionTimeMs}, "MemoryUsedKb" = {memoryUsedKb},
              "FinishedAt" = CURRENT_TIMESTAMP, "WorkerId" = NULL, "ClaimToken" = NULL, "LeaseExpiresAt" = NULL
            WHERE "Id" = {claim.RunId} AND "Status" = {(int)RunStatus.Running}
              AND "WorkerId" = {claim.WorkerId} AND "ClaimToken" = {claim.ClaimToken}
              AND "LeaseExpiresAt" > CURRENT_TIMESTAMP
            """, cancellationToken);
        return rows == 1;
    }

    public async Task<bool> AbandonClaimAsync(RunClaim claim, int maxAttempts, CancellationToken cancellationToken = default)
    {
        if (maxAttempts <= 0) throw new ArgumentOutOfRangeException(nameof(maxAttempts));
        var rows = await _context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "CodeRuns" SET "Status" = CASE WHEN "AttemptCount" >= {maxAttempts} THEN {(int)RunStatus.RuntimeError} ELSE {(int)RunStatus.Pending} END,
              "FinishedAt" = CASE WHEN "AttemptCount" >= {maxAttempts} THEN CURRENT_TIMESTAMP ELSE NULL END,
              "ErrorOutput" = CASE WHEN "AttemptCount" >= {maxAttempts} THEN 'Execution failed after retry limit.' ELSE NULL END,
              "WorkerId" = NULL, "ClaimToken" = NULL, "LeaseExpiresAt" = NULL
            WHERE "Id" = {claim.RunId} AND "Status" = {(int)RunStatus.Running}
              AND "WorkerId" = {claim.WorkerId} AND "ClaimToken" = {claim.ClaimToken}
            """, cancellationToken);
        return rows == 1;
    }

    private sealed class RunClaimRow { public Guid RunId { get; set; } public Guid ClaimToken { get; set; } public int AttemptCount { get; set; } public DateTime LeaseExpiresAt { get; set; } }

    private const string ClaimNextSql = """
        WITH exhausted AS (
          UPDATE "CodeRuns" SET "Status"=@runtimeErrorStatus, "FinishedAt"=CURRENT_TIMESTAMP,
            "ErrorOutput"='Execution failed after retry limit.', "WorkerId"=NULL, "ClaimToken"=NULL, "LeaseExpiresAt"=NULL
          WHERE "Status"=@runningStatus AND "LeaseExpiresAt" <= CURRENT_TIMESTAMP AND "AttemptCount" >= @maxAttempts RETURNING "Id"
        ), candidate AS (
          SELECT run."Id" FROM "CodeRuns" run
          WHERE (run."Status"=@pendingStatus AND run."AttemptCount" < @maxAttempts)
             OR (run."Status"=@runningStatus AND run."LeaseExpiresAt" <= CURRENT_TIMESTAMP AND run."AttemptCount" < @maxAttempts)
          ORDER BY CASE WHEN run."Status"=@runningStatus THEN 0 ELSE 1 END,
            CASE WHEN run."Status"=@runningStatus THEN run."LeaseExpiresAt" ELSE run."CreatedAt" END, run."Id"
          FOR UPDATE SKIP LOCKED LIMIT 1
        )
        UPDATE "CodeRuns" run SET "Status"=@runningStatus, "WorkerId"=@workerId, "ClaimToken"=@claimToken,
          "LeaseExpiresAt"=CURRENT_TIMESTAMP + @leaseDuration, "StartedAt"=COALESCE(run."StartedAt", CURRENT_TIMESTAMP),
          "FinishedAt"=NULL, "AttemptCount"=run."AttemptCount" + 1
        FROM candidate WHERE run."Id"=candidate."Id"
        RETURNING run."Id" AS "RunId", run."ClaimToken" AS "ClaimToken", run."AttemptCount" AS "AttemptCount", run."LeaseExpiresAt" AS "LeaseExpiresAt"
        """;
}
