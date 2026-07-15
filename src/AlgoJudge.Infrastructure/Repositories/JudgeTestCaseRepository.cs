using AlgoJudge.Application.Interfaces;
using AlgoJudge.Domain.Entities;
using AlgoJudge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AlgoJudge.Infrastructure.Repositories
{
    public class JudgeTestCaseRepository : IJudgeTestCaseRepository
    {
        private readonly AppDbContext _context;

        public JudgeTestCaseRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<JudgeTestCase>> GetByProblemIdAsync(int problemId)
        {
            return await _context.JudgeTestCases
                .AsNoTracking()
                .Where(testCase => testCase.ProblemId == problemId)
                .OrderBy(testCase => testCase.Ordinal)
                .ToListAsync();
        }
    }
}
