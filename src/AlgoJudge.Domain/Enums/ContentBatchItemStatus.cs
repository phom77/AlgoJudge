namespace AlgoJudge.Domain.Enums;

public enum ContentBatchItemStatus
{
    Pending = 0,
    Generating = 1,
    Ready = 2,
    Published = 3,
    Failed = 4,
    Retrying = 5,
    Skipped = 6
}
