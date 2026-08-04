public sealed class StockProfitValidator : InputValidator
{
    public override InputValidationResult Validate(JsonElement arguments)
    {
        if (!arguments.TryGetProperty("prices", out var prices) ||
            prices.ValueKind != JsonValueKind.Array || prices.GetArrayLength() is < 1 or > 100)
            return InputValidationResult.Invalid("prices must contain between 1 and 100 integers");
        foreach (var value in prices.EnumerateArray())
        {
            if (!value.TryGetInt32(out var number) || number is < 0 or > 10000)
                return InputValidationResult.Invalid("prices contains an out-of-range integer");
        }
        return InputValidationResult.Valid;
    }
}
