public sealed class LongestCommonPrefixValidator : InputValidator
{
    public override InputValidationResult Validate(JsonElement arguments)
    {
        if (!arguments.TryGetProperty("words", out var words) ||
            words.ValueKind != JsonValueKind.Array || words.GetArrayLength() is < 1 or > 20)
            return InputValidationResult.Invalid("words must contain between 1 and 20 strings");
        foreach (var value in words.EnumerateArray())
        {
            if (value.ValueKind != JsonValueKind.String) return InputValidationResult.Invalid("words contains a non-string value");
            var word = value.GetString()!;
            if (word.Length > 20 || word.Any(character => character is < 'a' or > 'z'))
                return InputValidationResult.Invalid("words contains an invalid lowercase string");
        }
        return InputValidationResult.Valid;
    }
}
