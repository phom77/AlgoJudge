namespace AlgoJudge.Application.Contracts.Admin;

public sealed class PublishContentBatchRequest
{
    public IReadOnlyList<Guid> RevisionIds { get; init; } = [];
}
