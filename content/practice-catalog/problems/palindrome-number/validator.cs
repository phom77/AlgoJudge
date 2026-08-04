public sealed class PalindromeNumberValidator : InputValidator
{
    public override InputValidationResult Validate(JsonElement arguments) =>
        arguments.TryGetProperty("x", out var x) &&
        x.TryGetInt32(out var value) && value is >= -1000000000 and <= 1000000000
            ? InputValidationResult.Valid
            : InputValidationResult.Invalid("x is outside the supported integer range");
}
