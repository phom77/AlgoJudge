namespace AlgoJudge.Application.Contracts.Admin;

public sealed class ContentBatchCountsResponse
{
    public int Total { get; init; }
    public int Pending { get; init; }
    public int Generating { get; init; }
    public int Ready { get; init; }
    public int Failed { get; init; }
    public int Published { get; init; }
    public int Skipped { get; init; }
}
