public sealed class RotateArrayValidator : InputValidator
{
    public override InputValidationResult Validate(JsonElement arguments)
    {
        if (!arguments.TryGetProperty("nums", out var nums) ||
            nums.ValueKind != JsonValueKind.Array || nums.GetArrayLength() is < 1 or > 100 ||
            !arguments.TryGetProperty("k", out var k) ||
            !k.TryGetInt32(out var rotations) || rotations is < 0 or > 1000)
            return InputValidationResult.Invalid("rotation arguments are invalid");
        foreach (var value in nums.EnumerateArray())
        {
            if (!value.TryGetInt32(out var number) || number is < -10000 or > 10000)
                return InputValidationResult.Invalid("nums contains an out-of-range integer");
        }
        return InputValidationResult.Valid;
    }
}
