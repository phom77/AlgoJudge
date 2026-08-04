public sealed class OverrideIdentityValidator : InputValidator
{
    public override InputValidationResult Validate(JsonElement arguments)
    {
        return arguments.TryGetProperty("value", out var value) &&
               value.TryGetInt32(out var number) &&
               number is >= 1 and <= 100
            ? InputValidationResult.Valid
            : InputValidationResult.Invalid("the overridden validator rejected value");
    }
}
