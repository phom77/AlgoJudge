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

        public async Task<TestCase?> GetByIdAsync(int id)
        {
            return await _context.TestCases.FindAsync(id);
        }

        public async Task AddAsync(TestCase testCase)
        {
            await _context.TestCases.AddAsync(testCase);
        }

        public async Task AddRangeAsync(IEnumerable<TestCase> testCases)
        {
            await _context.TestCases.AddRangeAsync(testCases);
        }

        public void Delete(TestCase testCase)
        {
            _context.TestCases.Remove(testCase);
        }
    }
}
