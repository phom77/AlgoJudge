using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AlgoJudge.Api.IntegrationTests;

internal static class OpenApiSnapshot
{
    public const string UpdateEnvironmentVariable = "UPDATE_OPENAPI_SNAPSHOT";
    public const string RelativePath =
        "tests/AlgoJudge.Api.IntegrationTests/Snapshots/openapi-v1.json";

    private static readonly JsonSerializerOptions CompactJsonOptions = new()
    {
        WriteIndented = false
    };

    private static readonly JsonSerializerOptions IndentedJsonOptions = new()
    {
        WriteIndented = true
    };

    private static readonly HashSet<string> SetLikeArrayProperties = new(
        [
            "allOf",
            "anyOf",
            "enum",
            "oneOf",
            "parameters",
            "required",
            "security",
            "tags",
            "type"
        ],
        StringComparer.Ordinal);

    public static string Canonicalize(string json)
    {
        var document = JsonNode.Parse(json)
            ?? throw new InvalidOperationException("OpenAPI document is empty.");
        var normalized = Normalize(document, isRoot: true)
            ?? throw new InvalidOperationException("OpenAPI document is empty.");
        return normalized
            .ToJsonString(IndentedJsonOptions)
            .Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
    }

    public static string GetSnapshotPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "AlgoJudge.slnx")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException(
                "Could not locate the repository root for the OpenAPI snapshot.");
        }

        return Path.Combine(
            directory.FullName,
            RelativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    public static async Task WriteAsync(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(
            path,
            content,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    public static string DescribeDifference(string expected, string actual)
    {
        var expectedLines = expected.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n');
        var actualLines = actual.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n');
        var sharedLength = Math.Min(expectedLines.Length, actualLines.Length);
        var firstDifference = 0;
        while (firstDifference < sharedLength &&
               expectedLines[firstDifference] == actualLines[firstDifference])
        {
            firstDifference++;
        }

        if (firstDifference == sharedLength && expectedLines.Length == actualLines.Length)
            return "No semantic difference was found.";

        var expectedLine = firstDifference < expectedLines.Length
            ? expectedLines[firstDifference]
            : "<end of snapshot>";
        var actualLine = firstDifference < actualLines.Length
            ? actualLines[firstDifference]
            : "<end of generated contract>";
        return $"First difference at line {firstDifference + 1}." +
               Environment.NewLine + $"Expected: {expectedLine}" +
               Environment.NewLine + $"Actual:   {actualLine}";
    }

    private static JsonNode? Normalize(
        JsonNode? node,
        bool isRoot = false,
        string? propertyName = null)
    {
        if (node is JsonObject jsonObject)
        {
            var normalizedObject = new JsonObject();
            foreach (var property in jsonObject.OrderBy(
                         property => property.Key,
                         StringComparer.Ordinal))
            {
                if (isRoot && property.Key == "servers")
                    continue;

                normalizedObject[property.Key] = Normalize(
                    property.Value,
                    propertyName: property.Key);
            }

            return normalizedObject;
        }

        if (node is JsonArray jsonArray)
        {
            IEnumerable<JsonNode?> normalizedItems = jsonArray
                .Select(item => Normalize(item));
            if (propertyName is not null &&
                SetLikeArrayProperties.Contains(propertyName))
            {
                normalizedItems = normalizedItems.OrderBy(
                    item => item?.ToJsonString(CompactJsonOptions) ?? "null",
                    StringComparer.Ordinal);
            }

            var normalizedArray = new JsonArray();
            foreach (var item in normalizedItems)
                normalizedArray.Add(item);
            return normalizedArray;
        }

        return node?.DeepClone();
    }
}
