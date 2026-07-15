using AlgoJudge.Application.DTOs.Common;
using AlgoJudge.Application.Interfaces;
using AlgoJudge.Domain.Entities;
using AlgoJudge.Domain.Enums;
using AlgoJudge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AlgoJudge.Infrastructure.Repositories
{
    public class ProblemRepository : IProblemRepository
    {
        private readonly AppDbContext _context;

        public ProblemRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Problem?> GetByIdAsync(int id)
        {
            return await _context.Problems.FindAsync(id);
        }

        public async Task<Problem?> GetPublishedBySlugAsync(string slug)
        {
            return await _context.Problems
                .AsNoTracking()
                .AsSplitQuery()
                .Where(problem =>
                    problem.Status == ProblemStatus.Published &&
                    problem.Slug == slug)
                .Include(problem => problem.Samples)
                .Include(problem => problem.Tags)
                    .ThenInclude(problemTag => problemTag.Tag)
                .SingleOrDefaultAsync();
        }

        public async Task<PagedResult<Problem>> GetPublishedPagedAsync(
            string? search,
            DifficultyLevel? difficulty,
            IReadOnlyCollection<string> tags,
            Guid? userId,
            bool? solved,
            int pageNumber,
            int pageSize)
        {
            var query = _context.Problems
                .AsNoTracking()
                .Where(problem => problem.Status == ProblemStatus.Published);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var escapedSearch = search
                    .Replace("\\", "\\\\", StringComparison.Ordinal)
                    .Replace("%", "\\%", StringComparison.Ordinal)
                    .Replace("_", "\\_", StringComparison.Ordinal);
                var pattern = $"%{escapedSearch}%";
                query = query.Where(problem =>
                    EF.Functions.ILike(problem.Title, pattern, "\\") ||
                    EF.Functions.ILike(problem.Slug, pattern, "\\"));
            }

            if (difficulty.HasValue)
                query = query.Where(problem => problem.Difficulty == difficulty.Value);

            foreach (var tag in tags)
            {
                var tagSlug = tag;
                query = query.Where(problem =>
                    problem.Tags.Any(problemTag => problemTag.Tag.Slug == tagSlug));
            }

            if (solved.HasValue && userId.HasValue)
            {
                query = solved.Value
                    ? query.Where(problem => problem.Submissions.Any(submission =>
                        submission.UserId == userId.Value &&
                        submission.Status == SubmissionStatus.Accepted))
                    : query.Where(problem => !problem.Submissions.Any(submission =>
                        submission.UserId == userId.Value &&
                        submission.Status == SubmissionStatus.Accepted));
            }

            var totalCount = await query.CountAsync();
            var items = await query
                .AsSplitQuery()
                .Include(problem => problem.Tags)
                    .ThenInclude(problemTag => problemTag.Tag)
                .OrderByDescending(problem => problem.CreatedAt)
                .ThenBy(problem => problem.Id)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<Problem>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }
    }
}
