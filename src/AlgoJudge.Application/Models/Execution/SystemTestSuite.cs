using AlgoJudge.Domain.Entities;
using AlgoJudge.Domain.Execution;
using AlgoJudge.Domain.Enums;

namespace AlgoJudge.Application.Models.Execution;

public sealed record SystemTestSuite(
    int ProblemId,
    int Version,
    IReadOnlyList<JudgeTestCase> TestCases,
    OutputCheckerConfiguration OutputChecker)
{
    public TestSuiteKind Kind => TestSuiteKind.System;
}
