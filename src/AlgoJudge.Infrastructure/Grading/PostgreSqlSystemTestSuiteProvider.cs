using AlgoJudge.Application.Interfaces;
using AlgoJudge.Application.Models.Execution;
using AlgoJudge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AlgoJudge.Infrastructure.Grading;

public sealed class PostgreSqlSystemTestSuiteProvider : ITestSuiteProvider
{
    private readonly AppDbContext _context;

    public PostgreSqlSystemTestSuiteProvider(AppDbContext context) => _context = context;

    public async Task<SystemTestSuite?> GetSystemSuiteAsync(
        int problemId,
        int version,
        CancellationToken cancellationToken = default)
    {
        if (problemId <= 0) throw new ArgumentOutOfRangeException(nameof(problemId));
        if (version <= 0) throw new ArgumentOutOfRangeException(nameof(version));

        var testCases = await _context.JudgeTestCases
            .AsNoTracking()
            .Where(testCase =>
                testCase.ProblemId == problemId &&
                testCase.SystemTestSuiteVersion == version)
            .OrderBy(testCase => testCase.Ordinal)
            .ToListAsync(cancellationToken);

        return testCases.Count == 0
            ? null
            : new SystemTestSuite(problemId, version, testCases);
    }
}
