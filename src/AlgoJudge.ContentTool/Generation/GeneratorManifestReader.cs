using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace AlgoJudge.ContentTool.Generation;

public sealed partial class GeneratorManifestReader
{
    public const string RelativeManifestPath = "generator/manifest.json";

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    private readonly int _maximumTestCaseCount;

    public GeneratorManifestReader(int maximumTestCaseCount)
    {
        if (maximumTestCaseCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumTestCaseCount));

        _maximumTestCaseCount = maximumTestCaseCount;
    }

    public async Task<GeneratorManifest> ReadAsync(
        string problemDirectory,
        CancellationToken cancellationToken = default)
    {
        var root = ResolveProblemDirectory(problemDirectory);
        var manifestPath = Path.Combine(root, "generator", "manifest.json");
        if (!File.Exists(manifestPath))
        {
            throw new TestGenerationException(
                $"Generator manifest is missing: {RelativeManifestPath}.");
        }

        string json;
        try
        {
            json = await File.ReadAllTextAsync(manifestPath, StrictUtf8, cancellationToken);
        }
        catch (DecoderFallbackException)
        {
            throw new TestGenerationException("Generator manifest is not valid UTF-8.");
        }

        GeneratorManifest? manifest;
        try
        {
            using var document = JsonDocument.Parse(json);
            EnsureNoDuplicateProperties(document.RootElement, "$");
            manifest = JsonSerializer.Deserialize<GeneratorManifest>(json, JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new TestGenerationException(
                $"Generator manifest is invalid near line {(exception.LineNumber ?? 0) + 1}.");
        }

        if (manifest is null)
            throw new TestGenerationException("Generator manifest cannot be empty.");

        Validate(manifest, root);
        return manifest;
    }

    internal static string ResolveProblemDirectory(string problemDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(problemDirectory);
        var root = Path.GetFullPath(problemDirectory);
        if (!Directory.Exists(root))
            throw new TestGenerationException($"Problem directory does not exist: {root}.");

        return root;
    }

    internal static string ResolveContainedFile(
        string root,
        string relativePath,
        string description)
    {
        if (string.IsNullOrWhiteSpace(relativePath) ||
            Path.IsPathRooted(relativePath) ||
            relativePath.Contains('\\'))
        {
            throw new TestGenerationException(
                $"{description} must be a forward-slash relative path.");
        }

        var segments = relativePath.Split('/');
        if (segments.Any(segment => segment.Length == 0 || segment is "." or ".."))
            throw new TestGenerationException($"{description} contains an unsafe path segment.");

        var fullPath = Path.GetFullPath(Path.Combine(root, Path.Combine(segments)));
        var rootPrefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(fullPath))
        {
            throw new TestGenerationException($"{description} does not exist inside the problem directory.");
        }

        return fullPath;
    }

    private void Validate(GeneratorManifest manifest, string root)
    {
        if (manifest.SchemaVersion != 1)
            throw new TestGenerationException("Generator manifest schemaVersion must be 1.");

        ValidateDotNetComponent(manifest.Generator, root, "generator");
        ValidateDotNetComponent(manifest.InputValidator, root, "inputValidator");

        if (manifest.Groups is null || manifest.Groups.Count == 0)
            throw new TestGenerationException("Generator manifest must define at least one group.");

        var names = new HashSet<string>(StringComparer.Ordinal);
        var total = 0;
        foreach (var group in manifest.Groups)
        {
            if (group is null || !GroupNamePattern().IsMatch(group.Name))
                throw new TestGenerationException("Generator group names must use lowercase kebab-case.");
            if (!names.Add(group.Name))
                throw new TestGenerationException($"Duplicate generator group: {group.Name}.");
            if (group.Count <= 0)
                throw new TestGenerationException($"Generator group {group.Name} count must be positive.");

            total = checked(total + group.Count);
            if (total > _maximumTestCaseCount)
            {
                throw new TestGenerationException(
                    $"Generator requests {total} test cases; the limit is {_maximumTestCaseCount}.");
            }
        }

        if (!string.Equals(manifest.ReferenceSolution.Type, "cpp17", StringComparison.Ordinal))
            throw new TestGenerationException("referenceSolution.type must be cpp17.");

        ResolveContainedFile(root, manifest.ReferenceSolution.Path, "Reference solution path");
    }

    private static void ValidateDotNetComponent(
        DotNetComponentManifest component,
        string root,
        string name)
    {
        if (component is null || !string.Equals(component.Type, "dotnet", StringComparison.Ordinal))
            throw new TestGenerationException($"{name}.type must be dotnet.");
        if (string.IsNullOrWhiteSpace(component.Entry))
            throw new TestGenerationException($"{name}.entry is required.");

        var assemblyPath = ResolveContainedFile(root, component.Assembly, $"{name}.assembly");
        if (!string.Equals(Path.GetExtension(assemblyPath), ".dll", StringComparison.OrdinalIgnoreCase))
            throw new TestGenerationException($"{name}.assembly must reference a .dll file.");
    }

    private static void EnsureNoDuplicateProperties(JsonElement element, string path)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var properties = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!properties.Add(property.Name))
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

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.NonBacktracking)]
    private static partial Regex GroupNamePattern();
}
