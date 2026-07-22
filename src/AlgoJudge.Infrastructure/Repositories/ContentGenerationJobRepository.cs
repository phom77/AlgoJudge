using System.Text.Json;
using AlgoJudge.Application.Interfaces;
using AlgoJudge.Application.Models.ContentGeneration;
using AlgoJudge.Domain.Entities;
using AlgoJudge.Domain.Enums;
using AlgoJudge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace AlgoJudge.Infrastructure.Repositories;

public sealed class ContentGenerationJobRepository : IContentGenerationJobRepository
{
    private readonly AppDbContext _context;
    public ContentGenerationJobRepository(AppDbContext context) => _context = context;

    public async Task<ContentGenerationClaim?> ClaimNextAsync(string workerId, TimeSpan leaseDuration, int maxAttempts, CancellationToken cancellationToken = default)
    {
        ValidateClaimArguments(workerId, leaseDuration, maxAttempts);
        await MarkExhaustedAsync(maxAttempts, cancellationToken);
        var token = Guid.NewGuid();
        var rows = await _context.Database.SqlQueryRaw<ClaimRow>(ClaimSql,
            IntegerParameter("pending", (int)ContentGenerationJobStatus.Pending),
            IntegerParameter("running", (int)ContentGenerationJobStatus.Running),
            new NpgsqlParameter("workerId", workerId), new NpgsqlParameter("claimToken", token),
            new NpgsqlParameter("leaseDuration", leaseDuration), IntegerParameter("maxAttempts", maxAttempts))
            .ToListAsync(cancellationToken);
        var row = rows.SingleOrDefault();
        return row is null ? null : new ContentGenerationClaim(row.JobId, row.RevisionId, row.ClaimToken,
            workerId, row.AttemptCount, row.LeaseExpiresAt, row.DefinitionSnapshotJson,
            row.DefinitionSha256, row.TimeLimitMs, row.MemoryLimitKb);
    }

    public async Task<bool> RenewLeaseAsync(ContentGenerationClaim claim, TimeSpan leaseDuration, CancellationToken cancellationToken = default)
    {
        if (leaseDuration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(leaseDuration));
        return await _context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "ContentGenerationJobs" SET "LeaseExpiresAt" = CURRENT_TIMESTAMP + {leaseDuration}
            WHERE "Id" = {claim.JobId} AND "Status" = {(int)ContentGenerationJobStatus.Running}
              AND "WorkerId" = {claim.WorkerId} AND "ClaimToken" = {claim.ClaimToken}
              AND "LeaseExpiresAt" > CURRENT_TIMESTAMP
            """, cancellationToken) == 1;
    }

    public async Task<bool> CompleteAsync(ContentGenerationClaim claim, ContentGenerationResult result, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var affected = await _context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "ContentGenerationJobs" SET "Status" = {(int)ContentGenerationJobStatus.Succeeded},
              "FinishedAt" = CURRENT_TIMESTAMP, "WorkerId" = NULL, "ClaimToken" = NULL, "LeaseExpiresAt" = NULL
            WHERE "Id" = {claim.JobId} AND "RevisionId" = {claim.RevisionId}
              AND "Status" = {(int)ContentGenerationJobStatus.Running} AND "WorkerId" = {claim.WorkerId}
              AND "ClaimToken" = {claim.ClaimToken} AND "LeaseExpiresAt" > CURRENT_TIMESTAMP
            """, cancellationToken);
        if (affected != 1) return false;
        await _context.AuthoringTestCases.Where(item => item.RevisionId == claim.RevisionId).ExecuteDeleteAsync(cancellationToken);
        await _context.AuthoringTestCases.AddRangeAsync(result.Cases.Select(item => new AuthoringTestCase
        {
            RevisionId = claim.RevisionId,
            Ordinal = item.Ordinal,
            Name = item.Name,
            Group = item.Group,
            Seed = item.Seed,
            Input = item.Input,
            ExpectedOutput = item.ExpectedOutput,
            KilledWrongSolutionsJson = JsonSerializer.Serialize(item.KilledWrongSolutions, JsonOptions)
        }), cancellationToken);
        var statistics = JsonSerializer.Serialize(new
        {
            result.CasesByGroup,
            result.WrongSolutionCount,
            result.KilledCaseCountByWrongSolution,
            result.SurvivingWrongSolutions
        }, JsonOptions);
        var revisionToken = Guid.NewGuid();
        affected = await _context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "ProblemAuthoringRevisions" SET "Status" = {(int)AuthoringRevisionStatus.Ready},
              "CandidateSuiteSha256" = {result.SuiteSha256}, "CandidateToolchain" = {result.Toolchain},
              "CandidateStatisticsJson" = CAST({statistics} AS jsonb), "CandidateCaseCount" = {result.Cases.Count},
              "UpdatedAt" = CURRENT_TIMESTAMP, "ConcurrencyToken" = {revisionToken}
            WHERE "Id" = {claim.RevisionId} AND "Status" = {(int)AuthoringRevisionStatus.Generating}
            """, cancellationToken);
        if (affected != 1) return false;
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public Task<bool> FailAsync(ContentGenerationClaim claim, string errorCode, string safeErrorMessage, CancellationToken cancellationToken = default) =>
        FinishFailedAsync(claim, errorCode, safeErrorMessage, cancellationToken);

    public async Task<bool> AbandonAsync(ContentGenerationClaim claim, int maxAttempts, CancellationToken cancellationToken = default)
    {
        var failed = claim.AttemptCount >= maxAttempts;
        var status = failed ? ContentGenerationJobStatus.Failed : ContentGenerationJobStatus.Pending;
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var affected = await _context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "ContentGenerationJobs" SET "Status" = {(int)status},
              "ErrorCode" = {(failed ? "attempts_exhausted" : null)},
              "ErrorMessage" = {(failed ? "Generation attempts were exhausted." : null)},
              "FinishedAt" = {(failed ? DateTime.UtcNow : (DateTime?)null)},
              "WorkerId" = NULL, "ClaimToken" = NULL, "LeaseExpiresAt" = NULL
            WHERE "Id" = {claim.JobId} AND "Status" = {(int)ContentGenerationJobStatus.Running}
              AND "WorkerId" = {claim.WorkerId} AND "ClaimToken" = {claim.ClaimToken}
            """, cancellationToken);
        if (affected == 1 && failed)
            await ReturnRevisionToDraftAsync(claim.RevisionId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return affected == 1;
    }

    private async Task<bool> FinishFailedAsync(ContentGenerationClaim claim, string code, string message, CancellationToken token)
    {
        code = Bound(code, 64); message = Bound(message, 1024);
        await using var transaction = await _context.Database.BeginTransactionAsync(token);
        var affected = await _context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "ContentGenerationJobs" SET "Status" = {(int)ContentGenerationJobStatus.Failed},
              "ErrorCode" = {code}, "ErrorMessage" = {message}, "FinishedAt" = CURRENT_TIMESTAMP,
              "WorkerId" = NULL, "ClaimToken" = NULL, "LeaseExpiresAt" = NULL
            WHERE "Id" = {claim.JobId} AND "Status" = {(int)ContentGenerationJobStatus.Running}
              AND "WorkerId" = {claim.WorkerId} AND "ClaimToken" = {claim.ClaimToken}
              AND "LeaseExpiresAt" > CURRENT_TIMESTAMP
            """, token);
        if (affected == 1) await ReturnRevisionToDraftAsync(claim.RevisionId, token);
        await transaction.CommitAsync(token);
        return affected == 1;
    }

    private Task ReturnRevisionToDraftAsync(Guid revisionId, CancellationToken token) =>
        _context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "ProblemAuthoringRevisions" SET "Status" = {(int)AuthoringRevisionStatus.Draft},
              "CandidateSuiteSha256" = NULL, "CandidateToolchain" = NULL,
              "CandidateStatisticsJson" = NULL, "CandidateCaseCount" = NULL, "UpdatedAt" = CURRENT_TIMESTAMP,
              "ConcurrencyToken" = {Guid.NewGuid()}
            WHERE "Id" = {revisionId} AND "Status" = {(int)AuthoringRevisionStatus.Generating}
            """, token);

    private async Task MarkExhaustedAsync(int maxAttempts, CancellationToken token)
    {
        await _context.Database.ExecuteSqlRawAsync("""
            WITH exhausted AS (
              UPDATE "ContentGenerationJobs" SET "Status" = 3, "ErrorCode" = 'attempts_exhausted',
                "ErrorMessage" = 'Generation attempts were exhausted.', "FinishedAt" = CURRENT_TIMESTAMP,
                "WorkerId" = NULL, "ClaimToken" = NULL, "LeaseExpiresAt" = NULL
              WHERE "Status" = 1 AND "LeaseExpiresAt" <= CURRENT_TIMESTAMP AND "AttemptCount" >= {0}
              RETURNING "RevisionId"
            )
            UPDATE "ProblemAuthoringRevisions" SET "Status" = 0, "UpdatedAt" = CURRENT_TIMESTAMP,
              "ConcurrencyToken" = gen_random_uuid()
            WHERE "Id" IN (SELECT "RevisionId" FROM exhausted) AND "Status" = 1
            """, [maxAttempts], token);
    }

    private static void ValidateClaimArguments(string workerId, TimeSpan lease, int maxAttempts)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);
        if (workerId.Length > 128 || lease <= TimeSpan.Zero || maxAttempts <= 0) throw new ArgumentException("Invalid generation claim arguments.");
    }
    private static string Bound(string value, int max) => string.IsNullOrWhiteSpace(value) ? "generation_failed" : value[..Math.Min(value.Length, max)];
    private static NpgsqlParameter IntegerParameter(string name, int value) =>
        new(name, NpgsqlDbType.Integer) { Value = value };

    private sealed class ClaimRow
    {
        public Guid JobId { get; set; }
        public Guid RevisionId { get; set; }
        public Guid ClaimToken { get; set; }
        public int AttemptCount { get; set; }
        public DateTime LeaseExpiresAt { get; set; }
        public string DefinitionSnapshotJson { get; set; } = string.Empty; public string DefinitionSha256 { get; set; } = string.Empty;
        public int TimeLimitMs { get; set; }
        public int MemoryLimitKb { get; set; }
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string ClaimSql = """
        WITH candidate AS (
          SELECT "Id" FROM "ContentGenerationJobs"
          WHERE ("Status" = @pending OR ("Status" = @running AND "LeaseExpiresAt" <= CURRENT_TIMESTAMP))
            AND "AttemptCount" < @maxAttempts
          ORDER BY "CreatedAt", "Id" FOR UPDATE SKIP LOCKED LIMIT 1
        )
        UPDATE "ContentGenerationJobs" AS job SET "Status" = @running, "WorkerId" = @workerId,
          "ClaimToken" = @claimToken, "LeaseExpiresAt" = CURRENT_TIMESTAMP + @leaseDuration,
          "AttemptCount" = job."AttemptCount" + 1, "StartedAt" = COALESCE(job."StartedAt", CURRENT_TIMESTAMP),
          "ErrorCode" = NULL, "ErrorMessage" = NULL
        FROM candidate WHERE job."Id" = candidate."Id"
        RETURNING job."Id" AS "JobId", job."RevisionId", job."ClaimToken", job."AttemptCount",
          job."LeaseExpiresAt", job."DefinitionSnapshotJson", job."DefinitionSha256",
          job."TimeLimitMs", job."MemoryLimitKb"
        """;
}
