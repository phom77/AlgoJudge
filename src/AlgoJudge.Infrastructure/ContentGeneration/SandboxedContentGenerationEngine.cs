using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AlgoJudge.Application.ContentGeneration;
using AlgoJudge.Application.FunctionExecution;
using AlgoJudge.Application.Interfaces;
using AlgoJudge.Application.Models.ContentGeneration;
using AlgoJudge.Infrastructure.Grading;
using Microsoft.Extensions.Configuration;

namespace AlgoJudge.Infrastructure.ContentGeneration;

public sealed class SandboxedContentGenerationEngine : IContentGenerationEngine
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };
    private readonly ISourceGenerationSandbox _sourceSandbox;
    private readonly IFunctionReferenceSolutionRunner _referenceRunner;
    private readonly IWrongSolutionRunner _wrongRunner;
    private readonly string _cppImage;
    private readonly int _maximumCases;

    public SandboxedContentGenerationEngine(ISourceGenerationSandbox sourceSandbox,
        IFunctionReferenceSolutionRunner referenceRunner, IWrongSolutionRunner wrongRunner,
        IConfiguration configuration)
    {
        _sourceSandbox = sourceSandbox; _referenceRunner = referenceRunner; _wrongRunner = wrongRunner;
        _cppImage = DockerSandboxOptions.FromConfiguration(configuration).Image;
        _maximumCases = configuration.GetValue("ContentGeneration:MaximumCaseCount", 500);
        if (_maximumCases is < 1 or > 5000) throw new InvalidOperationException("ContentGeneration:MaximumCaseCount must be between 1 and 5000.");
    }

    public async Task<ContentGenerationResult> GenerateAsync(ContentGenerationClaim claim, CancellationToken cancellationToken = default)
    {
        ProblemAuthoringDefinition definition;
        try
        {
            definition = JsonSerializer.Deserialize<ProblemAuthoringDefinition>(claim.DefinitionSnapshotJson, JsonOptions)
                ?? throw new JsonException();
        }
        catch (JsonException exception) { throw new ContentGenerationException("invalid_snapshot", "The authoring snapshot is invalid.", exception); }
        if (!string.Equals(Hash(claim.DefinitionSnapshotJson), claim.DefinitionSha256, StringComparison.Ordinal))
            throw new ContentGenerationException("invalid_snapshot", "The authoring snapshot hash does not match.");

        var request = new SourceGenerationRequest(definition.Generator.Source, definition.InputValidator.Source,
            unchecked((int)uint.Parse(claim.DefinitionSha256[..8], NumberStyles.HexNumber, CultureInfo.InvariantCulture)),
            _maximumCases, definition.FunctionSignature.Parameters.Select(item => item.Name).ToArray(),
            definition.HandwrittenCases.Select(item => new SourceHandwrittenCase(item.Name, item.Group, item.Arguments.GetRawText())).ToArray());
        SourceGenerationResult first;
        SourceGenerationResult second;
        try { first = await _sourceSandbox.GenerateAsync(request, cancellationToken); second = await _sourceSandbox.GenerateAsync(request, cancellationToken); }
        catch (Exception exception) when (exception is not OperationCanceledException)
        { throw new ContentGenerationException("generator_error", "Generator compilation or execution failed.", exception); }
        EnsureDeterministic(first, second);
        ValidateCases(first.Cases, definition.FunctionSignature);
        var inputs = first.Cases.Select(item => item.Input).ToArray();
        var limits = new ReferenceSolutionLimits(claim.TimeLimitMs, claim.MemoryLimitKb);
        var outputs = await _referenceRunner.RunFunctionAsync(definition.ReferenceSolution.Source,
            definition.FunctionSignature, inputs, limits, cancellationToken);
        var repeated = await _referenceRunner.RunFunctionAsync(definition.ReferenceSolution.Source,
            definition.FunctionSignature, inputs, limits, cancellationToken);
        if (!outputs.SequenceEqual(repeated, StringComparer.Ordinal) || outputs.Count != inputs.Length)
            throw new ContentGenerationException("nondeterministic_reference", "The reference solution is not deterministic.");
        ValidateOutputs(outputs, definition.FunctionSignature.ReturnType);

        var survivors = new List<string>();
        var wrongCoverage = new List<string>();
        var killedCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var killedByCase = Enumerable.Range(0, inputs.Length).Select(_ => new List<string>()).ToArray();
        foreach (var wrong in definition.WrongSolutions.OrderBy(item => item.Name, StringComparer.Ordinal))
        {
            var killed = await _wrongRunner.FindKilledCasesAsync(wrong.Source, definition.FunctionSignature,
                inputs, outputs, limits, cancellationToken);
            if (killed.Count == 0) survivors.Add(wrong.Name);
            killedCounts.Add(wrong.Name, killed.Count);
            foreach (var ordinal in killed)
            {
                if (ordinal <= 0 || ordinal > killedByCase.Length)
                    throw new ContentGenerationException("invalid_wrong_solution_result", "Wrong-solution analysis returned invalid data.");
                killedByCase[ordinal - 1].Add(wrong.Name);
            }
            wrongCoverage.Add($"{wrong.Name}|{Hash(wrong.Source)}|{killed.Count}");
        }
        var cases = first.Cases.Select((item, index) => new GeneratedContentCase(item.Ordinal, item.Name,
            item.Group, item.Seed, item.Input, outputs[index],
            killedByCase[index].Order(StringComparer.Ordinal).ToArray())).ToArray();
        var toolchain = $"{first.ToolchainIdentity}|{_cppImage}|content-worker-engine-v1";
        var material = new List<string> { claim.DefinitionSha256, toolchain, "json-exact-v1" };
        material.AddRange(wrongCoverage);
        material.AddRange(cases.Select(item => $"{item.Ordinal}|{item.Name}|{item.Group}|{item.Seed}|{Hash(item.Input)}|{Hash(item.ExpectedOutput)}|{string.Join(',', item.KilledWrongSolutions)}"));
        return new ContentGenerationResult(Hash(string.Join('\n', material)), toolchain, cases,
            cases.GroupBy(item => item.Group).ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal),
            definition.WrongSolutions.Count, killedCounts, survivors);
    }

    private static void EnsureDeterministic(SourceGenerationResult first, SourceGenerationResult second)
    {
        if (first.ToolchainIdentity != second.ToolchainIdentity || !first.Cases.SequenceEqual(second.Cases))
            throw new ContentGenerationException("nondeterministic_generator", "The generator is not deterministic.");
    }
    private static void ValidateCases(IReadOnlyList<SourceGeneratedCase> cases, FunctionSignature signature)
    {
        if (cases.Count == 0) throw new ContentGenerationException("empty_suite", "The generator produced no test cases.");
        var names = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < cases.Count; index++)
        {
            var item = cases[index];
            if (item.Ordinal != index + 1 || string.IsNullOrWhiteSpace(item.Name) || item.Name.Length > 160 || !names.Add(item.Name) ||
                item.Group is not ("handwritten" or "edge" or "random" or "adversarial" or "stress"))
                throw new ContentGenerationException("invalid_case", "The generator returned invalid case metadata.");
            try
            {
                using var document = JsonDocument.Parse(item.Input);
                if (document.RootElement.ValueKind != JsonValueKind.Object ||
                    document.RootElement.EnumerateObject().Count() != signature.Parameters.Count ||
                    signature.Parameters.Any(parameter => !document.RootElement.TryGetProperty(parameter.Name, out var value) ||
                        !FunctionValueJsonValidator.Matches(value, parameter.Type)))
                    throw new ContentGenerationException("invalid_case", $"Generated case {item.Ordinal} has invalid arguments.");
            }
            catch (JsonException exception) { throw new ContentGenerationException("invalid_case", $"Generated case {item.Ordinal} is invalid.", exception); }
        }
    }
    private static void ValidateOutputs(IReadOnlyList<string> outputs, FunctionValueType type)
    {
        for (var index = 0; index < outputs.Count; index++)
        {
            try { using var document = JsonDocument.Parse(outputs[index]); if (!FunctionValueJsonValidator.Matches(document.RootElement, type)) throw new JsonException(); }
            catch (JsonException exception) { throw new ContentGenerationException("invalid_reference_output", $"Reference output {index + 1} is invalid.", exception); }
        }
    }
    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
