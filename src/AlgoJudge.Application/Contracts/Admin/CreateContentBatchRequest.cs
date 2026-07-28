namespace AlgoJudge.Application.Contracts.Admin;

public sealed class CreateContentBatchRequest
{
    public string CatalogName { get; init; } = string.Empty;
    public IReadOnlyList<CreateContentBatchItemRequest> Items { get; init; } = [];
}
