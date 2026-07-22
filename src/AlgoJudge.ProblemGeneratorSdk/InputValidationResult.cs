namespace AlgoJudge.ProblemGeneratorSdk;

public sealed record InputValidationResult(bool IsValid, string? Error)
{
    public static InputValidationResult Valid { get; } = new(true, null);

    public static InputValidationResult Invalid(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        return new InputValidationResult(false, error);
    }
}
