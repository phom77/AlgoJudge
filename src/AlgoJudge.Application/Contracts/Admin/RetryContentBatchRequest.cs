namespace AlgoJudge.Application.Contracts.Admin;

public sealed class RetryContentBatchRequest
{
    public IReadOnlyList<Guid> ItemIds { get; init; } = [];
}
