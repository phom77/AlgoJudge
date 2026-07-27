using AlgoJudge.Domain.Enums;

namespace AlgoJudge.Application.Contracts.Admin;

public sealed class AdminProblemListQuery
{
    public string? Search { get; init; }
    public ProblemStatus? Status { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
