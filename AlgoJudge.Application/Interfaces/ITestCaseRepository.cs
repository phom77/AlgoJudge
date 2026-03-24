using AlgoJudge.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace AlgoJudge.Application.Interfaces
{
    public interface ITestCaseRepository
    {
        Task<IEnumerable<TestCase>> GetByProblemIdAsync(int problemId);
        Task<TestCase?> GetByIdAsync(int id);
        Task AddAsync(TestCase testCase);
        Task AddRangeAsync(IEnumerable<TestCase> testCases);
        void Delete(TestCase testCase);
    }
}
