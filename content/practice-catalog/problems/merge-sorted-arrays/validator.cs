public sealed class MergeSortedArraysValidator : InputValidator
{
    public override InputValidationResult Validate(JsonElement arguments)
    {
        if (!arguments.TryGetProperty("first", out var first) ||
            !arguments.TryGetProperty("second", out var second) ||
            !IsSortedArray(first) || !IsSortedArray(second) ||
            first.GetArrayLength() + second.GetArrayLength() < 1)
            return InputValidationResult.Invalid("inputs must be bounded sorted integer arrays with at least one value");
        return InputValidationResult.Valid;
    }

    private static bool IsSortedArray(JsonElement values)
    {
        if (values.ValueKind != JsonValueKind.Array || values.GetArrayLength() > 100) return false;
        int? previous = null;
        foreach (var value in values.EnumerateArray())
        {
            if (!value.TryGetInt32(out var number) || number is < -10000 or > 10000 ||
                previous.HasValue && number < previous.Value) return false;
            previous = number;
        }
        return true;
    }
}
