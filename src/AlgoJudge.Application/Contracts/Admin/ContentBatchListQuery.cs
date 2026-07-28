using AlgoJudge.Domain.Enums;

namespace AlgoJudge.Application.Contracts.Admin;

public sealed class ContentBatchListQuery
{
    public ContentBatchStatus? Status { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
