public sealed class MaximumSubarrayValidator : InputValidator
{
    public override InputValidationResult Validate(JsonElement arguments)
    {
        if (!arguments.TryGetProperty("nums", out var nums) ||
            nums.ValueKind != JsonValueKind.Array ||
            nums.GetArrayLength() is < 1 or > 100)
            return InputValidationResult.Invalid("nums must contain between 1 and 100 integers");

        foreach (var value in nums.EnumerateArray())
        {
            if (!value.TryGetInt32(out var number) || number is < -10000 or > 10000)
                return InputValidationResult.Invalid("nums contains an out-of-range integer");
        }
        return InputValidationResult.Valid;
    }
}
