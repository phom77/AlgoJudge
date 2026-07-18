using AlgoJudge.Application.ContentGeneration;
using AlgoJudge.ContentTool.Configuration;
using System.Globalization;
using System.Text;

namespace AlgoJudge.ContentTool.Generation;

public sealed class TestGenerationService
{
    private readonly ContentImportOptions _options;
    private readonly GeneratedTestSuiteStore _store = new();

    public TestGenerationService(ContentImportOptions options)
    {
        options.Validate();
        _options = options;
    }

    public async Task<TestGenerationResult> GenerateAsync(
        string problemDirectory,
        GeneratorManifest manifest,
        ITestCaseGenerator generator,
        IInputValidator inputValidator,
        IReferenceSolutionRunner referenceSolutionRunner,
        CancellationToken cancellationToken = default)
    {
        var root = GeneratorManifestReader.ResolveProblemDirectory(problemDirectory);
        var (testCases, generatedManifest) = await BuildAsync(
            root,
            manifest,
            generator,
            inputValidator,
            referenceSolutionRunner,
            cancellationToken);
        await _store.WriteAsync(root, testCases, generatedManifest, cancellationToken);
        return new TestGenerationResult(testCases.Count, generatedManifest.SuiteSha256);
    }

    public async Task<TestGenerationResult> ValidateGeneratedAsync(
        string problemDirectory,
        GeneratorManifest manifest,
        ITestCaseGenerator generator,
        IInputValidator inputValidator,
        IReferenceSolutionRunner referenceSolutionRunner,
        CancellationToken cancellationToken = default)
    {
        var root = GeneratorManifestReader.ResolveProblemDirectory(problemDirectory);
        var storedManifest = await _store.ReadManifestAsync(root, cancellationToken);
        var (testCases, expectedManifest) = await BuildAsync(
            root,
            manifest,
            generator,
            inputValidator,
            referenceSolutionRunner,
            cancellationToken);

        EnsureManifestsMatch(storedManifest, expectedManifest);
        foreach (var testCase in testCases)
        {
            var stem = testCase.Ordinal.ToString("D3", CultureInfo.InvariantCulture);
            var inputPath = Path.Combine(root, "tests", $"{stem}.in");
            var outputPath = Path.Combine(root, "tests", $"{stem}.out");
            if (!File.Exists(inputPath) || !File.Exists(outputPath))
                throw new TestGenerationException($"Generated case {testCase.Ordinal} is incomplete.");

            var input = await File.ReadAllTextAsync(inputPath, cancellationToken);
            var output = await File.ReadAllTextAsync(outputPath, cancellationToken);
            if (!string.Equals(input, testCase.Input, StringComparison.Ordinal) ||
                !string.Equals(output, testCase.Output, StringComparison.Ordinal))
            {
                throw new TestGenerationException(
                    $"Generated case {testCase.Ordinal} does not match its deterministic source.");
            }
        }

        var expectedFiles = testCases.Count * 2;
        var actualFiles = Directory.EnumerateFiles(Path.Combine(root, "tests")).Count();
        if (actualFiles != expectedFiles)
            throw new TestGenerationException("The tests directory contains unexpected files.");

        return new TestGenerationResult(testCases.Count, expectedManifest.SuiteSha256);
    }

    private async Task<(IReadOnlyList<GeneratedTestCase>, GeneratedSuiteManifest)> BuildAsync(
        string root,
        GeneratorManifest manifest,
        ITestCaseGenerator generator,
        IInputValidator inputValidator,
        IReferenceSolutionRunner referenceSolutionRunner,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(inputValidator);
        ArgumentNullException.ThrowIfNull(referenceSolutionRunner);

        if (manifest.Groups is null || manifest.Groups.Count == 0)
            throw new TestGenerationException("Generator manifest must define at least one group.");
        var requestedCount = manifest.Groups.Sum(group => (long)group.Count);
        if (requestedCount <= 0 || requestedCount > _options.MaxJudgeTestCaseCount)
        {
            throw new TestGenerationException(
                $"Generator test count must be between 1 and {_options.MaxJudgeTestCaseCount}.");
        }

        var generatedInputs = new List<(int Ordinal, string Group, int Seed, string Input)>();
        long totalBytes = 0;
        var ordinal = 1;
        foreach (var group in manifest.Groups)
        {
            for (var index = 0; index < group.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var seed = DeterministicSeed.Derive(group.Name, group.Seed, index);
                var context = new TestCaseGenerationContext(ordinal, group.Name, seed);
                var input = await generator.GenerateAsync(context, cancellationToken);
                var repeatedInput = await generator.GenerateAsync(context, cancellationToken);
                if (input is null || !string.Equals(input, repeatedInput, StringComparison.Ordinal))
                {
                    throw new TestGenerationException(
                        $"Generator is not deterministic for case {ordinal} (seed {seed}).");
                }

                EnsureContentWithinLimits(input, $"Generated input {ordinal}", ref totalBytes);
                var validation = await inputValidator.ValidateAsync(input, cancellationToken);
                if (validation is null || !validation.IsValid)
                {
                    var reason = string.IsNullOrWhiteSpace(validation?.Error)
                        ? "no reason supplied"
                        : validation.Error;
                    throw new TestGenerationException(
                        $"Generated input {ordinal} failed validation: {reason}.");
                }

                generatedInputs.Add((ordinal, group.Name, seed, input));
                ordinal++;
            }
        }

        var sourcePath = GeneratorManifestReader.ResolveContainedFile(
            root,
            manifest.ReferenceSolution.Path,
            "Reference solution path");
        var sourceCode = await File.ReadAllTextAsync(sourcePath, cancellationToken);
        var limits = await ProblemGenerationMetadataReader.ReadLimitsAsync(root, cancellationToken);
        if (limits.TimeLimitMs < _options.MinTimeLimitMs ||
            limits.TimeLimitMs > _options.MaxTimeLimitMs ||
            limits.MemoryLimitKb < _options.MinMemoryLimitKb ||
            limits.MemoryLimitKb > _options.MaxMemoryLimitKb)
        {
            throw new TestGenerationException(
                "problem.json execution limits are outside the configured package limits.");
        }
        var outputs = await referenceSolutionRunner.RunAsync(
            sourceCode,
            generatedInputs.Select(item => item.Input).ToArray(),
            limits,
            cancellationToken);
        if (outputs.Count != generatedInputs.Count)
            throw new TestGenerationException("Reference solution returned an unexpected output count.");

        var testCases = new List<GeneratedTestCase>(generatedInputs.Count);
        for (var index = 0; index < generatedInputs.Count; index++)
        {
            EnsureContentWithinLimits(outputs[index], $"Generated output {index + 1}", ref totalBytes);
            var input = generatedInputs[index];
            testCases.Add(new GeneratedTestCase(
                input.Ordinal,
                input.Group,
                input.Seed,
                input.Input,
                outputs[index]));
        }

        var generatedManifest = await BuildManifestAsync(root, manifest, testCases, cancellationToken);
        return (testCases, generatedManifest);
    }

    private async Task<GeneratedSuiteManifest> BuildManifestAsync(
        string root,
        GeneratorManifest manifest,
        IReadOnlyList<GeneratedTestCase> testCases,
        CancellationToken cancellationToken)
    {
        var manifestPath = Path.Combine(root, "generator", "manifest.json");
        var generatorAssembly = GeneratorManifestReader.ResolveContainedFile(
            root,
            manifest.Generator.Assembly,
            "generator.assembly");
        var validatorAssembly = GeneratorManifestReader.ResolveContainedFile(
            root,
            manifest.InputValidator.Assembly,
            "inputValidator.assembly");
        var referenceSolution = GeneratorManifestReader.ResolveContainedFile(
            root,
            manifest.ReferenceSolution.Path,
            "Reference solution path");

        var cases = testCases.Select(testCase => new GeneratedCaseManifest
        {
            Ordinal = testCase.Ordinal,
            Group = testCase.Group,
            Seed = testCase.Seed,
            InputSha256 = ContentHash.Sha256(testCase.Input),
            OutputSha256 = ContentHash.Sha256(testCase.Output)
        }).ToArray();
        var suiteMaterial = string.Join(
            '\n',
            cases.Select(item =>
                $"{item.Ordinal}|{item.Group}|{item.Seed}|{item.InputSha256}|{item.OutputSha256}"));

        return new GeneratedSuiteManifest
        {
            SchemaVersion = 1,
            GeneratorManifestSha256 = await ContentHash.Sha256FileAsync(manifestPath, cancellationToken),
            GeneratorAssemblySha256 = await ContentHash.Sha256FileAsync(generatorAssembly, cancellationToken),
            ValidatorAssemblySha256 = await ContentHash.Sha256FileAsync(validatorAssembly, cancellationToken),
            ReferenceSolutionSha256 = await ContentHash.Sha256FileAsync(referenceSolution, cancellationToken),
            SuiteSha256 = ContentHash.Sha256(suiteMaterial),
            Cases = cases
        };
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

    private static void EnsureManifestsMatch(
        GeneratedSuiteManifest stored,
        GeneratedSuiteManifest expected)
    {
        if (stored.SchemaVersion != expected.SchemaVersion ||
            !string.Equals(stored.GeneratorManifestSha256, expected.GeneratorManifestSha256, StringComparison.Ordinal) ||
            !string.Equals(stored.GeneratorAssemblySha256, expected.GeneratorAssemblySha256, StringComparison.Ordinal) ||
            !string.Equals(stored.ValidatorAssemblySha256, expected.ValidatorAssemblySha256, StringComparison.Ordinal) ||
            !string.Equals(stored.ReferenceSolutionSha256, expected.ReferenceSolutionSha256, StringComparison.Ordinal) ||
            !string.Equals(stored.SuiteSha256, expected.SuiteSha256, StringComparison.Ordinal) ||
            stored.Cases.Count != expected.Cases.Count)
        {
            throw new TestGenerationException("Generated suite manifest is stale.");
        }

        for (var index = 0; index < stored.Cases.Count; index++)
        {
            var actual = stored.Cases[index];
            var wanted = expected.Cases[index];
            if (actual.Ordinal != wanted.Ordinal ||
                actual.Seed != wanted.Seed ||
                !string.Equals(actual.Group, wanted.Group, StringComparison.Ordinal) ||
                !string.Equals(actual.InputSha256, wanted.InputSha256, StringComparison.Ordinal) ||
                !string.Equals(actual.OutputSha256, wanted.OutputSha256, StringComparison.Ordinal))
            {
                throw new TestGenerationException("Generated suite manifest is stale.");
            }
        }
    }
}
