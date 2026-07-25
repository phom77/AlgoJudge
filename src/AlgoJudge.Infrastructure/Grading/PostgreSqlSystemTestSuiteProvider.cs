using AlgoJudge.Application.Interfaces;
using AlgoJudge.Application.Models.Execution;
using AlgoJudge.Infrastructure.Data;
using AlgoJudge.Domain.Execution;
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

        var suite = await _context.SystemTestSuites
            .AsNoTracking()
            .Include(item => item.TestCases)
            .SingleOrDefaultAsync(item => item.ProblemId == problemId && item.Version == version,
                cancellationToken);

        return suite is null || suite.TestCases.Count == 0
            ? null
            : new SystemTestSuite(
                problemId,
                version,
                suite.TestCases.OrderBy(item => item.Ordinal).ToArray(),
                new OutputCheckerConfiguration(
                    suite.OutputCheckerKind,
                    suite.AbsoluteTolerance,
                    suite.RelativeTolerance));
    }
}
