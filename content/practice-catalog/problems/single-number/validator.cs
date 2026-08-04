public sealed class SingleNumberValidator : InputValidator
{
    public override InputValidationResult Validate(JsonElement arguments)
    {
        if (!arguments.TryGetProperty("nums", out var nums) ||
            nums.ValueKind != JsonValueKind.Array ||
            nums.GetArrayLength() is < 1 or > 99 ||
            nums.GetArrayLength() % 2 == 0)
        {
            return InputValidationResult.Invalid(
                "nums must contain an odd number of integers between 1 and 99");
        }

        var frequencies = new Dictionary<int, int>();
        foreach (var value in nums.EnumerateArray())
        {
            if (!value.TryGetInt32(out var number) || number is < -10000 or > 10000)
                return InputValidationResult.Invalid("nums contains an out-of-range integer");
            frequencies[number] = frequencies.GetValueOrDefault(number) + 1;
        }

        if (frequencies.Count(entry => entry.Value == 1) != 1 ||
            frequencies.Any(entry => entry.Value is not (1 or 2)))
        {
            return InputValidationResult.Invalid(
                "exactly one value must appear once and every other value exactly twice");
        }

        return InputValidationResult.Valid;
    }
}
