public sealed class ClimbingStairsValidator : InputValidator
{
    public override InputValidationResult Validate(JsonElement arguments) =>
        arguments.TryGetProperty("n", out var n) &&
        n.TryGetInt32(out var value) && value is >= 1 and <= 45
            ? InputValidationResult.Valid
            : InputValidationResult.Invalid("n must be an integer between 1 and 45");
}
