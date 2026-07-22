using System.Text.Json;
using System.Text.Json.Serialization;

namespace AlgoJudge.Application.FunctionExecution;

public static class FunctionSignatureJsonSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    public static FunctionSignature Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        try
        {
            return JsonSerializer.Deserialize<FunctionSignature>(json, JsonOptions)
                ?? throw new InvalidOperationException("Function signature cannot be null.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("Function signature JSON is invalid.", exception);
        }
    }

    public static string Serialize(FunctionSignature signature)
    {
        ArgumentNullException.ThrowIfNull(signature);
        return JsonSerializer.Serialize(signature, JsonOptions);
    }
}
