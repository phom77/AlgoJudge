using AlgoJudge.Application.DTOs.Common;
using AlgoJudge.Application.Interfaces;
using AlgoJudge.Application.Models.SubmissionQueue;
using AlgoJudge.Domain.Entities;
using AlgoJudge.Domain.Enums;
using AlgoJudge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AlgoJudge.Infrastructure.Repositories
{
    public class SubmissionRepository : ISubmissionRepository
    {
        private readonly AppDbContext _context;

        public SubmissionRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(Submission submission)
        {
            await _context.Submissions.AddAsync(submission);
        }

        public async Task<Submission?> GetByIdAsync(Guid id)
        {
            return await _context.Submissions.FindAsync(id);
        }

        public async Task<PagedResult<Submission>> GetPagedAsync(
            Guid userId,
            int? problemId,
            SubmissionStatus? status,
            int pageNumber,
            int pageSize)
        {
            var query = _context.Submissions
                .Where(s => s.UserId == userId);

            if (problemId.HasValue)
                query = query.Where(s => s.ProblemId == problemId.Value);

            if (status.HasValue)
                query = query.Where(s => s.Status == status.Value);

            var totalCount = await query.CountAsync();
            var items = await query
                .OrderByDescending(s => s.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<Submission>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        public Task<Submission?> GetClaimedAsync(
            SubmissionClaim claim,
            CancellationToken cancellationToken = default)
        {
            return _context.Submissions
                .AsNoTracking()
                .SingleOrDefaultAsync(submission =>
                    submission.Id == claim.SubmissionId &&
                    submission.Status == SubmissionStatus.Running &&
                    submission.WorkerId == claim.WorkerId &&
                    submission.ClaimToken == claim.ClaimToken,
                    cancellationToken);
        }

        public async Task<SubmissionClaim?> ClaimNextAsync(
            string workerId,
            TimeSpan leaseDuration,
            int maxAttempts,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(workerId);
            if (workerId.Length > 128)
                throw new ArgumentException("Worker ID cannot exceed 128 characters.", nameof(workerId));
            if (leaseDuration <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(leaseDuration));
            if (maxAttempts <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxAttempts));

            var claimToken = Guid.NewGuid();
            var rows = await _context.Database.SqlQueryRaw<SubmissionClaimRow>(
                ClaimNextSql,
                new NpgsqlParameter("pendingStatus", (int)SubmissionStatus.Pending),
                new NpgsqlParameter("runningStatus", (int)SubmissionStatus.Running),
                new NpgsqlParameter("runtimeErrorStatus", (int)SubmissionStatus.RuntimeError),
                new NpgsqlParameter("workerId", workerId),
                new NpgsqlParameter("claimToken", claimToken),
                new NpgsqlParameter("leaseDuration", leaseDuration),
                new NpgsqlParameter("maxAttempts", maxAttempts))
                .ToListAsync(cancellationToken);

            var row = rows.SingleOrDefault();
            return row is null
                ? null
                : new SubmissionClaim(
                    row.SubmissionId,
                    row.ClaimToken,
                    workerId,
                    row.AttemptCount,
                    row.LeaseExpiresAt);
        }

        public async Task<bool> RenewLeaseAsync(
            SubmissionClaim claim,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken = default)
        {
            if (leaseDuration <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(leaseDuration));

            var affectedRows = await _context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE "Submissions"
                SET "LeaseExpiresAt" = CURRENT_TIMESTAMP + {leaseDuration}
                WHERE "Id" = {claim.SubmissionId}
                  AND "Status" = {(int)SubmissionStatus.Running}
                  AND "WorkerId" = {claim.WorkerId}
                  AND "ClaimToken" = {claim.ClaimToken}
                  AND "LeaseExpiresAt" > CURRENT_TIMESTAMP
                """,
                cancellationToken);

            return affectedRows == 1;
        }

        public async Task<bool> FinalizeClaimAsync(
            SubmissionClaim claim,
            SubmissionStatus finalStatus,
            int executionTimeMs,
            int memoryUsedKb,
            CancellationToken cancellationToken = default)
        {
            if (!IsFinalStatus(finalStatus))
                throw new ArgumentException("A claim can only be finalized with a final verdict.", nameof(finalStatus));
            if (executionTimeMs < 0)
                throw new ArgumentOutOfRangeException(nameof(executionTimeMs));
            if (memoryUsedKb < 0)
                throw new ArgumentOutOfRangeException(nameof(memoryUsedKb));

            var affectedRows = await _context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE "Submissions"
                SET "Status" = {(int)finalStatus},
                    "ExecutionTime" = {executionTimeMs},
                    "MemoryUsed" = {memoryUsedKb},
                    "FinishedAt" = CURRENT_TIMESTAMP,
                    "WorkerId" = NULL,
                    "ClaimToken" = NULL,
                    "LeaseExpiresAt" = NULL
                WHERE "Id" = {claim.SubmissionId}
                  AND "Status" = {(int)SubmissionStatus.Running}
                  AND "WorkerId" = {claim.WorkerId}
                  AND "ClaimToken" = {claim.ClaimToken}
                  AND "LeaseExpiresAt" > CURRENT_TIMESTAMP
                """,
                cancellationToken);

            return affectedRows == 1;
        }

        public async Task<bool> AbandonClaimAsync(
            SubmissionClaim claim,
            int maxAttempts,
            CancellationToken cancellationToken = default)
        {
            if (maxAttempts <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxAttempts));

            var affectedRows = await _context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE "Submissions"
                SET "Status" = CASE
                        WHEN "AttemptCount" >= {maxAttempts}
                            THEN {(int)SubmissionStatus.RuntimeError}
                        ELSE {(int)SubmissionStatus.Pending}
                    END,
                    "FinishedAt" = CASE
                        WHEN "AttemptCount" >= {maxAttempts}
                            THEN CURRENT_TIMESTAMP
                        ELSE NULL
                    END,
                    "WorkerId" = NULL,
                    "ClaimToken" = NULL,
                    "LeaseExpiresAt" = NULL
                WHERE "Id" = {claim.SubmissionId}
                  AND "Status" = {(int)SubmissionStatus.Running}
                  AND "WorkerId" = {claim.WorkerId}
                  AND "ClaimToken" = {claim.ClaimToken}
                """,
                cancellationToken);

            return affectedRows == 1;
        }

        public async Task<IReadOnlyCollection<int>> GetSolvedProblemIdsAsync(
            Guid userId,
            IEnumerable<int> problemIds)
        {
            var ids = problemIds.Distinct().ToArray();
            if (ids.Length == 0)
                return Array.Empty<int>();

            return await _context.Submissions
                .AsNoTracking()
                .Where(submission =>
                    submission.UserId == userId &&
                    submission.Status == SubmissionStatus.Accepted &&
                    ids.Contains(submission.ProblemId))
                .Select(submission => submission.ProblemId)
                .Distinct()
                .ToListAsync();
        }

        public Task<bool> HasAcceptedSubmissionAsync(Guid userId, int problemId)
        {
            return _context.Submissions
                .AsNoTracking()
                .AnyAsync(submission =>
                    submission.UserId == userId &&
                    submission.ProblemId == problemId &&
                    submission.Status == SubmissionStatus.Accepted);
        }

        private static bool IsFinalStatus(SubmissionStatus status)
        {
            return status is
                SubmissionStatus.Accepted or
                SubmissionStatus.WrongAnswer or
                SubmissionStatus.TimeLimitExceeded or
                SubmissionStatus.MemoryLimitExceeded or
                SubmissionStatus.CompileError or
                SubmissionStatus.RuntimeError;
        }

        private sealed class SubmissionClaimRow
        {
            public Guid SubmissionId { get; set; }
            public Guid ClaimToken { get; set; }
            public int AttemptCount { get; set; }
            public DateTime LeaseExpiresAt { get; set; }
        }

        private const string ClaimNextSql = """
            WITH exhausted AS (
                UPDATE "Submissions"
                SET "Status" = @runtimeErrorStatus,
                    "FinishedAt" = CURRENT_TIMESTAMP,
                    "WorkerId" = NULL,
                    "ClaimToken" = NULL,
                    "LeaseExpiresAt" = NULL
                WHERE "Status" = @runningStatus
                  AND "LeaseExpiresAt" <= CURRENT_TIMESTAMP
                  AND "AttemptCount" >= @maxAttempts
                RETURNING "Id"
            ),
            candidate AS (
                SELECT submission."Id"
                FROM "Submissions" AS submission
                WHERE (
                    submission."Status" = @pendingStatus
                    AND submission."AttemptCount" < @maxAttempts
                ) OR (
                    submission."Status" = @runningStatus
                    AND submission."LeaseExpiresAt" <= CURRENT_TIMESTAMP
                    AND submission."AttemptCount" < @maxAttempts
                )
                ORDER BY
                    CASE WHEN submission."Status" = @runningStatus THEN 0 ELSE 1 END,
                    CASE
                        WHEN submission."Status" = @runningStatus
                            THEN submission."LeaseExpiresAt"
                        ELSE submission."CreatedAt"
                    END,
                    submission."Id"
                FOR UPDATE SKIP LOCKED
                LIMIT 1
            )
            UPDATE "Submissions" AS submission
            SET "Status" = @runningStatus,
                "WorkerId" = @workerId,
                "ClaimToken" = @claimToken,
                "LeaseExpiresAt" = CURRENT_TIMESTAMP + @leaseDuration,
                "StartedAt" = COALESCE(submission."StartedAt", CURRENT_TIMESTAMP),
                "FinishedAt" = NULL,
                "AttemptCount" = submission."AttemptCount" + 1
            FROM candidate
            WHERE submission."Id" = candidate."Id"
            RETURNING
                submission."Id" AS "SubmissionId",
                submission."ClaimToken" AS "ClaimToken",
                submission."AttemptCount" AS "AttemptCount",
                submission."LeaseExpiresAt" AS "LeaseExpiresAt"
            """;
    }
}
