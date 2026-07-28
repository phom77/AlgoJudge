using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AlgoJudge.ContentTool.Workspace;

internal static class WorkspaceJson
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static JsonSerializerOptions SerializerOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    public static async Task<(T Value, JsonElement Root)> ReadAsync<T>(
        string path,
        string description,
        long maximumBytes,
        IReadOnlyCollection<string> requiredProperties,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
            throw Error($"{description} is missing.");

        var length = new FileInfo(path).Length;
        if (length > maximumBytes)
            throw Error($"{description} exceeds the {maximumBytes}-byte limit.");

        string json;
        try
        {
            json = await File.ReadAllTextAsync(path, StrictUtf8, cancellationToken);
        }
        catch (DecoderFallbackException)
        {
            throw Error($"{description} is not valid UTF-8.");
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            EnsureNoDuplicateProperties(document.RootElement, "$");
            EnsureRequiredProperties(document.RootElement, description, requiredProperties);
            var value = JsonSerializer.Deserialize<T>(json, SerializerOptions)
                ?? throw new JsonException($"{description} cannot be empty.");
            return (value, document.RootElement.Clone());
        }
        catch (JsonException exception)
        {
            var line = exception.LineNumber is null
                ? string.Empty
                : $" near line {exception.LineNumber.Value + 1}";
            throw Error($"{description} is invalid{line}: {exception.Message}");
        }
    }

    public static WorkspaceValidationException Error(params string[] errors) => new(errors);

    private static void EnsureRequiredProperties(
        JsonElement root,
        string description,
        IReadOnlyCollection<string> requiredProperties)
    {
        if (root.ValueKind != JsonValueKind.Object)
            throw new JsonException($"{description} root must be an object.");

        foreach (var name in requiredProperties)
        {
            if (!root.TryGetProperty(name, out _))
                throw new JsonException($"{description} requires property {name}.");
        }
    }

    private static void EnsureNoDuplicateProperties(JsonElement element, string path)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                    throw new JsonException($"Duplicate JSON property {path}.{property.Name}.");
                EnsureNoDuplicateProperties(property.Value, $"{path}.{property.Name}");
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in element.EnumerateArray())
                EnsureNoDuplicateProperties(item, $"{path}[{index++}]");
        }
    }
}
