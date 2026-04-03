using AlgoJudge.Application.DTOs.Leaderboard;
using AlgoJudge.Application.Interfaces;
using AlgoJudge.Domain.Enums;
using AlgoJudge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AlgoJudge.Infrastructure.Repositories
{
    public class LeaderboardRepository : ILeaderboardRepository
    {
        private readonly AppDbContext _context;

        public LeaderboardRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<LeaderboardEntryDto>> GetGlobalLeaderboardAsync(
            int pageNumber, int pageSize)
        {
            var skip = (pageNumber - 1) * pageSize;

            return await _context.Submissions
                .Where(s => s.Status == SubmissionStatus.Accepted)
                .GroupBy(s => new { s.UserId, s.ProblemId })
                .Select(g => new
                {
                    g.Key.UserId,
                    g.Key.ProblemId
                })
                .GroupBy(x => x.UserId)
                .Select(g => new
                {
                    UserId = g.Key,
                    SolvedCount = g.Count()
                })
                .Join(
                    _context.Submissions
                        .Where(s => s.Status == SubmissionStatus.Accepted)
                        .GroupBy(s => new { s.UserId, s.ProblemId })
                        .Select(g => new { g.Key.UserId, g.Key.ProblemId })
                        .Join(
                            _context.Problems,
                            s => s.ProblemId,
                            p => p.Id,
                            (s, p) => new { s.UserId, p.Score }
                        )
                        .GroupBy(x => x.UserId)
                        .Select(g => new { UserId = g.Key, TotalScore = g.Sum(x => x.Score) }),
                    left => left.UserId,
                    right => right.UserId,
                    (left, right) => new { left.UserId, left.SolvedCount, right.TotalScore }
                )
                .Join(
                    _context.Users,
                    x => x.UserId,
                    u => u.Id,
                    (x, u) => new LeaderboardEntryDto
                    {
                        Rank = 0,
                        UserId = u.Id,
                        UserName = u.UserName,
                        FullName = u.FullName,
                        TotalScore = x.TotalScore,
                        SolvedCount = x.SolvedCount
                    }
                )
                .OrderByDescending(e => e.TotalScore)
                .ThenBy(e => e.UserName)
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> GetTotalStudentCountAsync()
        {
            return await _context.Submissions
                .Where(s => s.Status == SubmissionStatus.Accepted)
                .Select(s => s.UserId)
                .Distinct()
                .CountAsync();
        }
    }
}