using AlgoJudge.Domain.Entities;
using AlgoJudge.Domain.Enums;

namespace AlgoJudge.Application.Models.Execution;

public sealed record SystemTestSuite(
    int ProblemId,
    int Version,
    IReadOnlyList<JudgeTestCase> TestCases)
{
    public TestSuiteKind Kind => TestSuiteKind.System;
}
