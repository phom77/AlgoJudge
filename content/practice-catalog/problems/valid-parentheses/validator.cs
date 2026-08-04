public sealed class ValidParenthesesValidator : InputValidator
{
    public override InputValidationResult Validate(JsonElement arguments)
    {
        if (!arguments.TryGetProperty("s", out var value) || value.ValueKind != JsonValueKind.String)
            return InputValidationResult.Invalid("s must be a string");
        var text = value.GetString()!;
        if (text.Length is < 1 or > 100 || text.Any(character => "()[]{}".IndexOf(character) < 0))
            return InputValidationResult.Invalid("s contains invalid bracket data");
        return InputValidationResult.Valid;
    }
}
