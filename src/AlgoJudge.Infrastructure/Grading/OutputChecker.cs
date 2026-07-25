using System.Globalization;
using System.Text.Json;
using AlgoJudge.Application.Interfaces;
using AlgoJudge.Domain.Enums;
using AlgoJudge.Domain.Execution;

namespace AlgoJudge.Infrastructure.Grading;

public sealed class OutputChecker : IOutputChecker
{
    public bool IsMatch(
        OutputCheckerConfiguration configuration,
        string expectedOutput,
        string actualOutput)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(expectedOutput);
        ArgumentNullException.ThrowIfNull(actualOutput);
        configuration.Validate();

        return configuration.Kind switch
        {
            OutputCheckerKind.TokenExact => TokenExact(expectedOutput, actualOutput),
            OutputCheckerKind.JsonExact => JsonExact(expectedOutput, actualOutput),
            OutputCheckerKind.FloatingPoint => FloatingPoint(
                expectedOutput,
                actualOutput,
                configuration.AbsoluteTolerance!.Value,
                configuration.RelativeTolerance!.Value),
            _ => throw new InvalidOperationException("The output checker kind is invalid.")
        };
    }

    private static bool TokenExact(string expected, string actual) =>
        Tokens(expected).SequenceEqual(Tokens(actual), StringComparer.Ordinal);

    private static bool JsonExact(string expected, string actual)
    {
        try
        {
            using var expectedDocument = JsonDocument.Parse(expected);
            using var actualDocument = JsonDocument.Parse(actual);
            return JsonEquals(expectedDocument.RootElement, actualDocument.RootElement);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool FloatingPoint(
        string expected,
        string actual,
        double absoluteTolerance,
        double relativeTolerance)
    {
        var expectedTokens = Tokens(expected);
        var actualTokens = Tokens(actual);
        if (expectedTokens.Length != actualTokens.Length)
            return false;

        for (var index = 0; index < expectedTokens.Length; index++)
        {
            if (!TryReadFiniteDouble(expectedTokens[index], out var expectedValue) ||
                !TryReadFiniteDouble(actualTokens[index], out var actualValue))
            {
                return false;
            }

            var difference = Math.Abs(expectedValue - actualValue);
            var scale = Math.Max(Math.Abs(expectedValue), Math.Abs(actualValue));
            if (difference > absoluteTolerance &&
                (scale == 0 || Math.Abs(expectedValue / scale - actualValue / scale) > relativeTolerance))
                return false;
        }

        return true;
    }

    private static string[] Tokens(string value) => value.Split(
        (char[]?)null,
        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static bool TryReadFiniteDouble(string value, out double parsed) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) &&
        double.IsFinite(parsed);

    private static bool JsonEquals(JsonElement expected, JsonElement actual)
    {
        if (expected.ValueKind != actual.ValueKind)
            return false;

        return expected.ValueKind switch
        {
            JsonValueKind.Object => JsonObjectEquals(expected, actual),
            JsonValueKind.Array => JsonArrayEquals(expected, actual),
            JsonValueKind.String => expected.GetString() == actual.GetString(),
            JsonValueKind.Number => expected.GetRawText() == actual.GetRawText(),
            JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null => true,
            _ => false
        };
    }

    private static bool JsonObjectEquals(JsonElement expected, JsonElement actual)
    {
        var expectedProperties = expected.EnumerateObject().ToArray();
        if (expectedProperties.Length != actual.EnumerateObject().Count())
            return false;

        return expectedProperties.All(property =>
            actual.TryGetProperty(property.Name, out var actualValue) &&
            JsonEquals(property.Value, actualValue));
    }

    private static bool JsonArrayEquals(JsonElement expected, JsonElement actual)
    {
        var expectedItems = expected.EnumerateArray();
        var actualItems = actual.EnumerateArray();
        while (expectedItems.MoveNext())
        {
            if (!actualItems.MoveNext() || !JsonEquals(expectedItems.Current, actualItems.Current))
                return false;
        }

        return !actualItems.MoveNext();
    }
}
