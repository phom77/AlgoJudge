using AlgoJudge.Application.Interfaces;
using AlgoJudge.Domain.Entities;
using AlgoJudge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace AlgoJudge.Infrastructure.Repositories
{
    public class TestCaseRepository : ITestCaseRepository
    {
        private readonly AppDbContext _context;

        public TestCaseRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<TestCase>> GetByProblemIdAsync(int problemId)
        {
            return await _context.TestCases
                .Where(tc => tc.ProblemId == problemId)
                .ToListAsync();
        }
    }
}
