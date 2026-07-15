using AlgoJudge.Application.DTOs.TestCase;
using System;
using System.Collections.Generic;
using System.Text;

namespace AlgoJudge.Application.Interfaces
{
    public interface ITestCaseService
    {
        Task<TestCaseDto> CreateAsync(int problemId, CreateTestCaseDto dto);
        Task<IEnumerable<TestCaseDto>> CreateBulkAsync(int problemId, Stream zipStream);
        Task<IEnumerable<TestCaseDto>> GetByProblemIdAsync(int problemId, bool includeHidden);
        Task DeleteAsync(int problemId, int testCaseId);
    }
}
