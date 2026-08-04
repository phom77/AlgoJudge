public sealed class IdentityValidator : InputValidator
{
    public override InputValidationResult Validate(JsonElement arguments)
    {
        if (!arguments.TryGetProperty("value", out var value) ||
            value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt32(out var number) ||
            number is < 1 or > 100)
        {
            return InputValidationResult.Invalid("value must be an integer between 1 and 100");
        }

        return InputValidationResult.Valid;
    }
}
