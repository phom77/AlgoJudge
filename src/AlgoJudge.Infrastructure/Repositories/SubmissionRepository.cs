using AlgoJudge.Application.DTOs.Common;
using AlgoJudge.Application.Interfaces;
using AlgoJudge.Domain.Entities;
using AlgoJudge.Domain.Enums;
using AlgoJudge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

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

        public async Task<IEnumerable<Submission>> GetPendingAsync()
        {
            return await _context.Submissions
                .Where(s => s.Status == SubmissionStatus.Pending)
                .OrderBy(s => s.CreatedAt)
                .Take(10)
                .ToListAsync();
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
    }
}
