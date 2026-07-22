using AlgoJudge.Application.ContentGeneration;

namespace AlgoJudge.Application.Contracts.Admin;

public sealed class UpdateHandwrittenCasesRequest
{
    public IReadOnlyList<HandwrittenCaseDefinition> Cases { get; init; } = [];
}
