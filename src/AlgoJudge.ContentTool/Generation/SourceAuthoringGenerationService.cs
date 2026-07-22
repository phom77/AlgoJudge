using System.Globalization;
using System.Text;
using System.Text.Json;
using AlgoJudge.Application.ContentGeneration;
using AlgoJudge.Application.FunctionExecution;
using AlgoJudge.ContentTool.Configuration;

namespace AlgoJudge.ContentTool.Generation;

public sealed class SourceAuthoringGenerationService
{
    private readonly ContentImportOptions _options;
    private readonly ISourceGenerationSandbox _sourceSandbox;
    private readonly IFunctionReferenceSolutionRunner _referenceRunner;
    private readonly IWrongSolutionRunner _wrongSolutionRunner;
    private readonly IFunctionHarnessBuilder _functionHarnessBuilder;
    private readonly string _cpp17ToolchainIdentity;
    private readonly GeneratedTestSuiteStore _store = new();

    public SourceAuthoringGenerationService(
        ContentImportOptions options,
        ISourceGenerationSandbox sourceSandbox,
        IFunctionReferenceSolutionRunner referenceRunner,
        IWrongSolutionRunner wrongSolutionRunner,
        IFunctionHarnessBuilder functionHarnessBuilder,
        string cpp17ToolchainIdentity)
    {
        options.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(cpp17ToolchainIdentity);
        _options = options;
        _sourceSandbox = sourceSandbox;
        _referenceRunner = referenceRunner;
        _wrongSolutionRunner = wrongSolutionRunner;
        _functionHarnessBuilder = functionHarnessBuilder;
        _cpp17ToolchainIdentity = cpp17ToolchainIdentity;
    }

    public async Task<TestGenerationResult> GenerateAsync(
        string problemDirectory,
        ProblemAuthoringDocument document,
        CancellationToken cancellationToken = default)
    {
        var root = GeneratorManifestReader.ResolveProblemDirectory(problemDirectory);
        var (testCases, manifest) = await BuildAsync(root, document, cancellationToken);
        Directory.CreateDirectory(Path.Combine(root, "generator"));
        await _store.WriteAsync(root, testCases, manifest, cancellationToken);
        await WriteCompatibilityFunctionFilesAsync(
            root,
            document.Definition.FunctionSignature,
            cancellationToken);
        return CreateResult(testCases.Count, manifest);
    }

    public async Task<TestGenerationResult> ValidateGeneratedAsync(
        string problemDirectory,
        ProblemAuthoringDocument document,
        CancellationToken cancellationToken = default)
    {
        var root = GeneratorManifestReader.ResolveProblemDirectory(problemDirectory);
        var stored = await _store.ReadManifestAsync(root, cancellationToken);
        var (testCases, expected) = await BuildAsync(root, document, cancellationToken);
        EnsureManifestMatches(stored, expected);
        await EnsureFilesMatchAsync(root, testCases, cancellationToken);
        await EnsureCompatibilityFunctionFilesMatchAsync(
            root,
            document.Definition.FunctionSignature,
            cancellationToken);
        return CreateResult(testCases.Count, expected);
    }

    private async Task<(IReadOnlyList<GeneratedTestCase>, GeneratedSuiteManifest)> BuildAsync(
        string root,
        ProblemAuthoringDocument document,
        CancellationToken cancellationToken)
    {
        var definition = document.Definition;
        var request = new SourceGenerationRequest(
            definition.Generator.Source,
            definition.InputValidator.Source,
            DeriveRootSeed(document.DefinitionSha256),
            _options.MaxJudgeTestCaseCount,
            definition.FunctionSignature.Parameters.Select(parameter => parameter.Name).ToArray(),
            definition.HandwrittenCases.Select(testCase => new SourceHandwrittenCase(
                testCase.Name,
                testCase.Group,
                testCase.Arguments.GetRawText())).ToArray());

        var first = await RunGeneratorAsync(request, cancellationToken);
        var repeated = await RunGeneratorAsync(request, cancellationToken);
        EnsureDeterministic(first, repeated);
        ValidateCases(first.Cases, definition.FunctionSignature);

        var limits = await ProblemGenerationMetadataReader.ReadLimitsAsync(root, cancellationToken);
        EnsureLimits(limits);
        var inputs = first.Cases.Select(testCase => testCase.Input).ToArray();
        var outputs = await _referenceRunner.RunFunctionAsync(
            definition.ReferenceSolution.Source,
            definition.FunctionSignature,
            inputs,
            limits,
            cancellationToken);
        var repeatedOutputs = await _referenceRunner.RunFunctionAsync(
            definition.ReferenceSolution.Source,
            definition.FunctionSignature,
            inputs,
            limits,
            cancellationToken);
        if (!outputs.SequenceEqual(repeatedOutputs, StringComparer.Ordinal))
            throw new TestGenerationException("Reference solution is not deterministic.");
        if (outputs.Count != inputs.Length)
            throw new TestGenerationException("Reference solution returned an unexpected output count.");
        ValidateOutputs(outputs, definition.FunctionSignature.ReturnType);

        var killedByCase = Enumerable.Range(0, inputs.Length)
            .Select(_ => new List<string>())
            .ToArray();
        var wrongManifests = new List<WrongSolutionManifest>();
        foreach (var wrongSolution in definition.WrongSolutions)
        {
            var killed = await _wrongSolutionRunner.FindKilledCasesAsync(
                wrongSolution.Source,
                definition.FunctionSignature,
                inputs,
                outputs,
                limits,
                cancellationToken);
            foreach (var ordinal in killed)
            {
                if (ordinal <= 0 || ordinal > killedByCase.Length)
                    throw new TestGenerationException("Wrong-solution runner returned an invalid case ordinal.");
                killedByCase[ordinal - 1].Add(wrongSolution.Name);
            }
            wrongManifests.Add(new WrongSolutionManifest
            {
                Name = wrongSolution.Name,
                SourceSha256 = ContentHash.Sha256(wrongSolution.Source),
                KilledCaseCount = killed.Count
            });
        }

        var generatedCases = new List<GeneratedTestCase>(inputs.Length);
        long totalBytes = 0;
        for (var index = 0; index < inputs.Length; index++)
        {
            EnsureContentWithinLimits(inputs[index], $"Generated input {index + 1}", ref totalBytes);
            EnsureContentWithinLimits(outputs[index], $"Generated output {index + 1}", ref totalBytes);
            var sourceCase = first.Cases[index];
            generatedCases.Add(new GeneratedTestCase(
                sourceCase.Ordinal,
                sourceCase.Group,
                sourceCase.Seed,
                sourceCase.Input,
                outputs[index]));
        }

        var caseManifests = first.Cases.Select((sourceCase, index) => new GeneratedCaseManifest
        {
            Ordinal = sourceCase.Ordinal,
            Name = sourceCase.Name,
            Group = sourceCase.Group,
            Seed = sourceCase.Seed,
            InputSha256 = ContentHash.Sha256(sourceCase.Input),
            OutputSha256 = ContentHash.Sha256(outputs[index]),
            KilledWrongSolutions = killedByCase[index].Order(StringComparer.Ordinal).ToArray()
        }).ToArray();
        var survivors = wrongManifests
            .Where(item => item.KilledCaseCount == 0)
            .Select(item => item.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var suiteMaterial = BuildSuiteMaterial(
            document,
            first.ToolchainIdentity,
            caseManifests,
            wrongManifests);
        var manifest = new GeneratedSuiteManifest
        {
            SchemaVersion = 2,
            AuthoringDefinitionSha256 = document.DefinitionSha256,
            GeneratorSourceSha256 = ContentHash.Sha256(definition.Generator.Source),
            ValidatorSourceSha256 = ContentHash.Sha256(definition.InputValidator.Source),
            ReferenceSolutionSha256 = ContentHash.Sha256(definition.ReferenceSolution.Source),
            GenerationToolchain =
                $"{first.ToolchainIdentity}|{_cpp17ToolchainIdentity}|source-engine-v1",
            GeneratorSdkVersion = definition.Generator.SdkVersion,
            Comparator = "json-exact-v1",
            WrongSolutions = wrongManifests.OrderBy(item => item.Name, StringComparer.Ordinal).ToArray(),
            SurvivingWrongSolutions = survivors,
            Cases = caseManifests,
            SuiteSha256 = ContentHash.Sha256(suiteMaterial)
        };
        return (generatedCases, manifest);
    }

    private async Task<SourceGenerationResult> RunGeneratorAsync(
        SourceGenerationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _sourceSandbox.GenerateAsync(request, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            throw new TestGenerationException(exception.Message);
        }
    }

    private static void EnsureDeterministic(
        SourceGenerationResult first,
        SourceGenerationResult repeated)
    {
        if (!string.Equals(first.ToolchainIdentity, repeated.ToolchainIdentity, StringComparison.Ordinal) ||
            first.Cases.Count != repeated.Cases.Count)
            throw new TestGenerationException("Generator is not deterministic.");
        for (var index = 0; index < first.Cases.Count; index++)
        {
            if (first.Cases[index] != repeated.Cases[index])
                throw new TestGenerationException("Generator is not deterministic.");
        }
    }

    private static void ValidateCases(
        IReadOnlyList<SourceGeneratedCase> cases,
        FunctionSignature signature)
    {
        if (cases.Count == 0)
            throw new TestGenerationException("Generator produced no test cases.");
        for (var index = 0; index < cases.Count; index++)
        {
            var testCase = cases[index];
            if (testCase.Ordinal != index + 1 || string.IsNullOrWhiteSpace(testCase.Name) ||
                testCase.Group is not ("handwritten" or "edge" or "random" or "adversarial" or "stress"))
                throw new TestGenerationException("Generator returned invalid case metadata.");
            try
            {
                using var document = JsonDocument.Parse(testCase.Input);
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object ||
                    root.EnumerateObject().Count() != signature.Parameters.Count)
                    throw new TestGenerationException($"Generated case {testCase.Ordinal} has invalid arguments.");
                foreach (var parameter in signature.Parameters)
                {
                    if (!root.TryGetProperty(parameter.Name, out var value) ||
                        !FunctionValueJsonValidator.Matches(value, parameter.Type))
                        throw new TestGenerationException($"Generated case {testCase.Ordinal} has invalid arguments.");
                }
            }
            catch (JsonException)
            {
                throw new TestGenerationException($"Generated case {testCase.Ordinal} is not valid JSON.");
            }
        }
    }

    private static void ValidateOutputs(
        IReadOnlyList<string> outputs,
        FunctionValueType returnType)
    {
        for (var index = 0; index < outputs.Count; index++)
        {
            try
            {
                using var document = JsonDocument.Parse(outputs[index]);
                if (!FunctionValueJsonValidator.Matches(document.RootElement, returnType))
                    throw new TestGenerationException($"Reference output {index + 1} has the wrong type.");
            }
            catch (JsonException)
            {
                throw new TestGenerationException($"Reference output {index + 1} is not valid JSON.");
            }
        }
    }

    private void EnsureLimits(ReferenceSolutionLimits limits)
    {
        if (limits.TimeLimitMs < _options.MinTimeLimitMs ||
            limits.TimeLimitMs > _options.MaxTimeLimitMs ||
            limits.MemoryLimitKb < _options.MinMemoryLimitKb ||
            limits.MemoryLimitKb > _options.MaxMemoryLimitKb)
            throw new TestGenerationException("problem.json execution limits are outside configured bounds.");
    }

    private void EnsureContentWithinLimits(string content, string name, ref long totalBytes)
    {
        var bytes = Encoding.UTF8.GetByteCount(content);
        if (bytes > _options.MaxEntryBytes)
            throw new TestGenerationException($"{name} exceeds the {_options.MaxEntryBytes}-byte limit.");
        totalBytes = checked(totalBytes + bytes);
        if (totalBytes > _options.MaxTotalUncompressedBytes)
            throw new TestGenerationException("Generated tests exceed the total content size limit.");
    }

    private static int DeriveRootSeed(string definitionHash) =>
        unchecked((int)uint.Parse(definitionHash[..8], NumberStyles.HexNumber, CultureInfo.InvariantCulture));

    private string BuildSuiteMaterial(
        ProblemAuthoringDocument document,
        string generatorToolchain,
        IReadOnlyList<GeneratedCaseManifest> cases,
        IReadOnlyList<WrongSolutionManifest> wrongSolutions)
    {
        var lines = new List<string>
        {
            document.DefinitionSha256,
            ContentHash.Sha256(document.Definition.Generator.Source),
            ContentHash.Sha256(document.Definition.InputValidator.Source),
            ContentHash.Sha256(document.Definition.ReferenceSolution.Source),
            generatorToolchain,
            _cpp17ToolchainIdentity,
            "source-engine-v1",
            $"sdk={document.Definition.Generator.SdkVersion}",
            "comparator=json-exact-v1"
        };
        lines.AddRange(wrongSolutions.OrderBy(item => item.Name, StringComparer.Ordinal)
            .Select(item => $"wrong|{item.Name}|{item.SourceSha256}|{item.KilledCaseCount}"));
        lines.AddRange(cases.Select(item =>
            $"case|{item.Ordinal}|{item.Name}|{item.Group}|{item.Seed}|{item.InputSha256}|" +
            $"{item.OutputSha256}|{string.Join(',', item.KilledWrongSolutions)}"));
        return string.Join('\n', lines);
    }

    private static void EnsureManifestMatches(
        GeneratedSuiteManifest stored,
        GeneratedSuiteManifest expected)
    {
        if (stored.SchemaVersion != 2 ||
            !string.Equals(stored.SuiteSha256, expected.SuiteSha256, StringComparison.Ordinal) ||
            !string.Equals(stored.AuthoringDefinitionSha256, expected.AuthoringDefinitionSha256, StringComparison.Ordinal) ||
            !string.Equals(stored.GenerationToolchain, expected.GenerationToolchain, StringComparison.Ordinal))
            throw new TestGenerationException("Generated suite manifest is stale.");
    }

    private static async Task EnsureFilesMatchAsync(
        string root,
        IReadOnlyList<GeneratedTestCase> testCases,
        CancellationToken cancellationToken)
    {
        var testsPath = Path.Combine(root, "tests");
        foreach (var testCase in testCases)
        {
            var stem = testCase.Ordinal.ToString("D3", CultureInfo.InvariantCulture);
            var inputPath = Path.Combine(testsPath, $"{stem}.in");
            var outputPath = Path.Combine(testsPath, $"{stem}.out");
            if (!File.Exists(inputPath) || !File.Exists(outputPath) ||
                !string.Equals(await File.ReadAllTextAsync(inputPath, cancellationToken), testCase.Input, StringComparison.Ordinal) ||
                !string.Equals(await File.ReadAllTextAsync(outputPath, cancellationToken), testCase.Output, StringComparison.Ordinal))
                throw new TestGenerationException($"Generated case {testCase.Ordinal} does not match its deterministic source.");
        }
        if (Directory.EnumerateFiles(testsPath).Count() != testCases.Count * 2)
            throw new TestGenerationException("The tests directory contains unexpected files.");
    }

    private static TestGenerationResult CreateResult(
        int count,
        GeneratedSuiteManifest manifest) =>
        new(count, manifest.SuiteSha256)
        {
            SurvivingWrongSolutionCount = manifest.SurvivingWrongSolutions.Count
        };

    private async Task WriteCompatibilityFunctionFilesAsync(
        string root,
        FunctionSignature signature,
        CancellationToken cancellationToken)
    {
        var functionDirectory = Path.Combine(root, "function");
        Directory.CreateDirectory(functionDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(functionDirectory, "signature.json"),
            FunctionSignatureJsonSerializer.Serialize(signature) + "\n",
            new UTF8Encoding(false),
            cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(functionDirectory, "adapter-template.cpp"),
            _functionHarnessBuilder.BuildLegacyTemplate(signature),
            new UTF8Encoding(false),
            cancellationToken);
    }

    private async Task EnsureCompatibilityFunctionFilesMatchAsync(
        string root,
        FunctionSignature signature,
        CancellationToken cancellationToken)
    {
        var functionDirectory = Path.Combine(root, "function");
        var signaturePath = Path.Combine(functionDirectory, "signature.json");
        var adapterPath = Path.Combine(functionDirectory, "adapter-template.cpp");
        var expectedSignature = FunctionSignatureJsonSerializer.Serialize(signature) + "\n";
        var expectedAdapter = _functionHarnessBuilder.BuildLegacyTemplate(signature);
        if (!File.Exists(signaturePath) || !File.Exists(adapterPath) ||
            !string.Equals(
                await File.ReadAllTextAsync(signaturePath, cancellationToken),
                expectedSignature,
                StringComparison.Ordinal) ||
            !string.Equals(
                await File.ReadAllTextAsync(adapterPath, cancellationToken),
                expectedAdapter,
                StringComparison.Ordinal))
            throw new TestGenerationException("Generated Function compatibility files are stale.");
    }
}
