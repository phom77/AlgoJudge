public sealed class LeftmostBinarySearchValidator : InputValidator
{
    public override InputValidationResult Validate(JsonElement arguments)
    {
        if (!arguments.TryGetProperty("nums", out var nums) ||
            nums.ValueKind != JsonValueKind.Array || nums.GetArrayLength() is < 1 or > 100 ||
            !arguments.TryGetProperty("target", out var target) ||
            !target.TryGetInt32(out var targetValue) || targetValue is < -10000 or > 10000)
            return InputValidationResult.Invalid("binary-search arguments are invalid");

        int? previous = null;
        foreach (var value in nums.EnumerateArray())
        {
            if (!value.TryGetInt32(out var number) || number is < -10000 or > 10000 ||
                previous.HasValue && number < previous.Value)
                return InputValidationResult.Invalid("nums must be a sorted in-range integer array");
            previous = number;
        }
        return InputValidationResult.Valid;
    }
}
