namespace AlgoJudge.Application.ContentGeneration;

public sealed record InputValidationResult(bool IsValid, string? Error)
{
    public static InputValidationResult Valid { get; } = new(true, null);

    public static InputValidationResult Invalid(string error) =>
        new(false, error);
}
