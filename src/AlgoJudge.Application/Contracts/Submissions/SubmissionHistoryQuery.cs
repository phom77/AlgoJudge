using AlgoJudge.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace AlgoJudge.Application.Contracts.Submissions;

public sealed class SubmissionHistoryQuery
{
    [Range(1, int.MaxValue, ErrorMessage = "Problem ID must be greater than zero.")]
    public int? ProblemId { get; set; }

    public SubmissionStatus? Status { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Page number must be at least 1.")]
    public int PageNumber { get; set; } = 1;

    [Range(1, 100, ErrorMessage = "Page size must be between 1 and 100.")]
    public int PageSize { get; set; } = 20;
}
