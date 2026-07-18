using System.Text.Json;

namespace AlgoJudge.Application.FunctionExecution;

public static class FunctionValueJsonValidator
{
    public static bool Matches(JsonElement value, FunctionValueType type) => type switch
    {
        FunctionValueType.Int32 => value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out _),
        FunctionValueType.Int64 => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
        FunctionValueType.Double =>
            value.ValueKind == JsonValueKind.Number &&
            value.TryGetDouble(out var number) &&
            double.IsFinite(number),
        FunctionValueType.Boolean => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
        FunctionValueType.String => value.ValueKind == JsonValueKind.String,
        FunctionValueType.Int32Array => MatchesArray(value, FunctionValueType.Int32),
        FunctionValueType.Int64Array => MatchesArray(value, FunctionValueType.Int64),
        FunctionValueType.DoubleArray => MatchesArray(value, FunctionValueType.Double),
        FunctionValueType.BooleanArray => MatchesArray(value, FunctionValueType.Boolean),
        FunctionValueType.StringArray => MatchesArray(value, FunctionValueType.String),
        _ => false
    };

    private static bool MatchesArray(JsonElement value, FunctionValueType itemType) =>
        value.ValueKind == JsonValueKind.Array &&
        value.EnumerateArray().All(item => Matches(item, itemType));
}
