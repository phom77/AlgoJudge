using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using AlgoJudge.Application.ContentGeneration;
using AlgoJudge.Application.FunctionExecution;
using AlgoJudge.ContentTool.Configuration;
using AlgoJudge.ContentTool.Packages;
using AlgoJudge.Domain.Enums;

namespace AlgoJudge.ContentTool.Generation;

public sealed partial class ProblemAuthoringDefinitionReader
{
    public const string RelativeDefinitionPath = "authoring.json";

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    private readonly ContentImportOptions _options;

    public ProblemAuthoringDefinitionReader(ContentImportOptions options)
    {
        options.Validate();
        _options = options;
    }

    public async Task<ProblemAuthoringDocument> ReadAsync(
        string problemDirectory,
        CancellationToken cancellationToken = default)
    {
        var root = GeneratorManifestReader.ResolveProblemDirectory(problemDirectory);
        var path = Path.Combine(root, RelativeDefinitionPath);
        if (!File.Exists(path))
            throw new TestGenerationException($"Authoring definition is missing: {RelativeDefinitionPath}.");

        string json;
        try
        {
            json = await File.ReadAllTextAsync(path, StrictUtf8, cancellationToken);
        }
        catch (DecoderFallbackException)
        {
            throw new TestGenerationException("Authoring definition is not valid UTF-8.");
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            EnsureNoDuplicateProperties(document.RootElement, "$");
            EnsureRequiredProperties(document.RootElement);
            var definition = JsonSerializer.Deserialize<ProblemAuthoringDefinition>(json, JsonOptions)
                ?? throw new TestGenerationException("Authoring definition cannot be empty.");
            Validate(definition, document.RootElement);
            return new ProblemAuthoringDocument(definition, ContentHash.Sha256(json));
        }
        catch (JsonException exception)
        {
            throw new TestGenerationException(
                $"Authoring definition is invalid near line {(exception.LineNumber ?? 0) + 1}.");
        }
    }

    private void Validate(ProblemAuthoringDefinition definition, JsonElement root)
    {
        var errors = new List<string>();
        if (definition.SchemaVersion != 1)
            errors.Add("schemaVersion must be 1");
        if (definition.ExecutionMode != ProblemExecutionMode.Function)
            errors.Add("executionMode must be Function");

        var signatureJson = root.GetProperty("functionSignature").GetRawText();
        var signatureErrors = new List<string>();
        var signature = FunctionPackageValidator.ParseSignature(signatureJson, signatureErrors);
        errors.AddRange(signatureErrors.Select(error => $"functionSignature: {error}"));

        ValidateGeneratorSource(definition.Generator, "generator", errors);
        ValidateGeneratorSource(definition.InputValidator, "inputValidator", errors);
        ValidateFunctionSource(definition.ReferenceSolution, "referenceSolution", errors);

        if (definition.HandwrittenCases is null || definition.HandwrittenCases.Count == 0)
            errors.Add("at least one handwritten case is required");
        else if (definition.HandwrittenCases.Count > _options.MaxJudgeTestCaseCount)
            errors.Add($"handwritten case count exceeds {_options.MaxJudgeTestCaseCount}");

        var caseNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var testCase in definition.HandwrittenCases ?? [])
        {
            if (testCase is null || !NamePattern().IsMatch(testCase.Name))
            {
                errors.Add("handwritten case names must use lowercase kebab-case");
                continue;
            }
            if (!caseNames.Add(testCase.Name))
                errors.Add($"duplicate handwritten case name: {testCase.Name}");
            if (testCase.Group is not ("handwritten" or "edge" or "adversarial" or "stress"))
                errors.Add($"handwritten case {testCase.Name} has an invalid group");
            if (signature is not null)
                ValidateArguments(signature, testCase.Arguments, testCase.Name, errors);
        }

        var wrongNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var wrongSolution in definition.WrongSolutions ?? [])
        {
            if (wrongSolution is null || !NamePattern().IsMatch(wrongSolution.Name))
            {
                errors.Add("wrong-solution names must use lowercase kebab-case");
                continue;
            }
            if (!wrongNames.Add(wrongSolution.Name))
                errors.Add($"duplicate wrong solution name: {wrongSolution.Name}");
            if (!string.Equals(wrongSolution.Language, "cpp17", StringComparison.Ordinal))
                errors.Add($"wrong solution {wrongSolution.Name} language must be cpp17");
            ValidateSource(wrongSolution.Source, $"wrong solution {wrongSolution.Name}", errors);
        }

        if (errors.Count > 0)
        {
            throw new TestGenerationException(
                $"Authoring definition is invalid: {string.Join("; ", errors)}.");
        }
    }

    private void ValidateGeneratorSource(
        GeneratorSourceDefinition source,
        string name,
        ICollection<string> errors)
    {
        if (source is null || !string.Equals(source.Language, "csharp", StringComparison.Ordinal))
        {
            errors.Add($"{name}.language must be csharp");
            return;
        }
        if (source.SdkVersion != 1)
            errors.Add($"{name}.sdkVersion must be 1");
        ValidateSource(source.Source, name, errors);
    }

    private void ValidateFunctionSource(
        FunctionSourceDefinition source,
        string name,
        ICollection<string> errors)
    {
        if (source is null || !string.Equals(source.Language, "cpp17", StringComparison.Ordinal))
        {
            errors.Add($"{name}.language must be cpp17");
            return;
        }
        ValidateSource(source.Source, name, errors);
    }

    private void ValidateSource(string source, string name, ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(source))
            errors.Add($"{name}.source is required");
        else if (Encoding.UTF8.GetByteCount(source) > _options.MaxEntryBytes)
            errors.Add($"{name}.source exceeds the {_options.MaxEntryBytes}-byte limit");
    }

    private static void ValidateArguments(
        FunctionSignature signature,
        JsonElement arguments,
        string caseName,
        ICollection<string> errors)
    {
        if (arguments.ValueKind != JsonValueKind.Object)
        {
            errors.Add($"handwritten case {caseName} arguments must be an object");
            return;
        }

        var properties = arguments.EnumerateObject().ToArray();
        if (properties.Length != signature.Parameters.Count)
            errors.Add($"handwritten case {caseName} must contain every argument exactly once");
        foreach (var parameter in signature.Parameters)
        {
            if (!arguments.TryGetProperty(parameter.Name, out var value))
                errors.Add($"handwritten case {caseName} is missing argument {parameter.Name}");
            else if (!FunctionValueJsonValidator.Matches(value, parameter.Type))
                errors.Add($"handwritten case {caseName} argument {parameter.Name} has the wrong type");
        }
        foreach (var property in properties)
        {
            if (!signature.Parameters.Any(parameter => parameter.Name == property.Name))
                errors.Add($"handwritten case {caseName} contains unknown argument {property.Name}");
        }
    }

    private static void EnsureRequiredProperties(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            throw new JsonException("Authoring definition root must be an object.");
        foreach (var property in new[]
                 {
                     "schemaVersion", "executionMode", "functionSignature",
                     "handwrittenCases", "generator", "inputValidator", "referenceSolution"
                 })
        {
            if (!root.TryGetProperty(property, out _))
                throw new JsonException($"Authoring definition requires property {property}.");
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

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.NonBacktracking)]
    private static partial Regex NamePattern();
}
