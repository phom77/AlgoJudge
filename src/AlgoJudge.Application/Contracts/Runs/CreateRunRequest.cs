using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace AlgoJudge.Application.Contracts.Runs;

public sealed class CreateRunRequest
{
    [Required(ErrorMessage = "Source code is required.")]
    public string SourceCode { get; init; } = string.Empty;

    [Required(ErrorMessage = "Language is required.")]
    [RegularExpression("^cpp17$", ErrorMessage = "Language must be 'cpp17'.")]
    public string Language { get; init; } = string.Empty;

    public string? Input { get; init; }
    public JsonElement? Arguments { get; init; }
}
