using AlgoJudge.Application.ContentGeneration;

namespace AlgoJudge.Application.Contracts.Admin;

public sealed class UpdateSuiteQualityPolicyRequest
{
    public SuiteQualityPolicy QualityPolicy { get; init; } = new();
}
