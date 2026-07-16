using System.ComponentModel.DataAnnotations;

namespace AlgoJudge.Application.Contracts.Submissions;

public sealed class CreateSubmissionRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Problem ID must be greater than zero.")]
    public int ProblemId { get; set; }

    [Required(ErrorMessage = "Source code is required.")]
    [StringLength(SubmissionContractLimits.MaxSourceCodeBytes,
        MinimumLength = 1,
        ErrorMessage = "Source code must not exceed 65536 characters.")]
    public string SourceCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Language is required.")]
    [RegularExpression("^cpp17$", ErrorMessage = "Language must be 'cpp17'.")]
    public string Language { get; set; } = string.Empty;
}
