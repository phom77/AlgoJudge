namespace AlgoJudge.Domain.Enums;

public enum ContentBatchStatus
{
    Created = 0,
    Validating = 1,
    Generating = 2,
    ReadyForReview = 3,
    Publishing = 4,
    Completed = 5
}
