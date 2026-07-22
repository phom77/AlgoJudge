using System.Text.Json;
using AlgoJudge.Application.ContentGeneration;
using AlgoJudge.Application.FunctionExecution;
using AlgoJudge.ContentTool.Configuration;
using AlgoJudge.ContentTool.Generation;
using AlgoJudge.Infrastructure.Grading;

namespace AlgoJudge.ContentTool.Tests;

public sealed class SourceAuthoringGenerationServiceTests
{
    [Fact]
    public async Task GenerateCreatesDeterministicSuiteWithDifferentialProvenance()
    {
        using var directory = ProblemAuthoringDefinitionReaderTests.AuthoringDirectory.Create(
            DefinitionWithWrongSolutions());
        var document = await new ProblemAuthoringDefinitionReader(new ContentImportOptions())
            .ReadAsync(directory.Path);
        var sandbox = new StubSourceSandbox(ValidCases());
        var reference = new StubReferenceRunner(["1", "2"]);
        var wrongRunner = new StubWrongSolutionRunner();
        var service = CreateService(sandbox, reference, wrongRunner);

        var result = await service.GenerateAsync(directory.Path, document);
        var validated = await service.ValidateGeneratedAsync(directory.Path, document);

        Assert.Equal(2, result.TestCaseCount);
        Assert.Equal(1, result.SurvivingWrongSolutionCount);
        Assert.Equal(result, validated);
        Assert.Equal(4, sandbox.InvocationCount);
        Assert.Equal(4, reference.InvocationCount);
        Assert.True(File.Exists(Path.Combine(directory.Path, "tests", "001.in")));
        Assert.True(File.Exists(Path.Combine(directory.Path, "function", "signature.json")));
        Assert.Contains(
            "{{USER_SOURCE}}",
            await File.ReadAllTextAsync(Path.Combine(
                directory.Path,
                "function",
                "adapter-template.cpp")),
            StringComparison.Ordinal);
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(
            Path.Combine(directory.Path, "generator", "generated-tests.json")));
        Assert.Equal(2, manifest.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(
            "off-by-one",
            manifest.RootElement.GetProperty("cases")[1]
                .GetProperty("killedWrongSolutions")[0].GetString());
        Assert.Equal(
            "survivor",
            manifest.RootElement.GetProperty("survivingWrongSolutions")[0].GetString());
    }

    [Fact]
    public async Task NonDeterministicSandboxOutputIsRejectedBeforeReferenceRuns()
    {
        using var directory = ProblemAuthoringDefinitionReaderTests.AuthoringDirectory.Create(
            ProblemAuthoringDefinitionReaderTests.ValidDefinition());
        var document = await new ProblemAuthoringDefinitionReader(new ContentImportOptions())
            .ReadAsync(directory.Path);
        var sandbox = new AlternatingSourceSandbox();
        var reference = new StubReferenceRunner(["1"]);
        var service = CreateService(sandbox, reference, new StubWrongSolutionRunner());

        var exception = await Assert.ThrowsAsync<TestGenerationException>(() =>
            service.GenerateAsync(directory.Path, document));

        Assert.Contains("not deterministic", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, reference.InvocationCount);
    }

    [Fact]
    public async Task GeneratedArgumentsAreValidatedAgainstFunctionSignature()
    {
        using var directory = ProblemAuthoringDefinitionReaderTests.AuthoringDirectory.Create(
            ProblemAuthoringDefinitionReaderTests.ValidDefinition());
        var document = await new ProblemAuthoringDefinitionReader(new ContentImportOptions())
            .ReadAsync(directory.Path);
        var invalid = new[]
        {
            new SourceGeneratedCase(1, "bad", "random", 1, "{\"value\":\"wrong\"}")
        };
        var reference = new StubReferenceRunner(["1"]);
        var service = CreateService(
            new StubSourceSandbox(invalid),
            reference,
            new StubWrongSolutionRunner());

        var exception = await Assert.ThrowsAsync<TestGenerationException>(() =>
            service.GenerateAsync(directory.Path, document));

        Assert.Contains("invalid arguments", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, reference.InvocationCount);
    }

    [Fact]
    public async Task NonDeterministicReferenceSolutionIsRejected()
    {
        using var directory = ProblemAuthoringDefinitionReaderTests.AuthoringDirectory.Create(
            ProblemAuthoringDefinitionReaderTests.ValidDefinition());
        var document = await new ProblemAuthoringDefinitionReader(new ContentImportOptions())
            .ReadAsync(directory.Path);
        var reference = new AlternatingReferenceRunner();
        var service = CreateService(
            new StubSourceSandbox([ValidCases()[0]]),
            reference,
            new StubWrongSolutionRunner());

        var exception = await Assert.ThrowsAsync<TestGenerationException>(() =>
            service.GenerateAsync(directory.Path, document));

        Assert.Contains("Reference solution is not deterministic", exception.Message, StringComparison.Ordinal);
    }

    private static SourceAuthoringGenerationService CreateService(
        ISourceGenerationSandbox sourceSandbox,
        IFunctionReferenceSolutionRunner referenceRunner,
        IWrongSolutionRunner wrongSolutionRunner) =>
        new(
            new ContentImportOptions(),
            sourceSandbox,
            referenceRunner,
            wrongSolutionRunner,
            new Cpp17FunctionHarnessBuilder(),
            "algojudge/judge-cpp17:14.3.0-v1");

    private static IReadOnlyList<SourceGeneratedCase> ValidCases() =>
    [
        new SourceGeneratedCase(1, "minimum", "handwritten", 0, "{\"value\":1}"),
        new SourceGeneratedCase(2, "random-1", "random", 42, "{\"value\":2}")
    ];

    private static string DefinitionWithWrongSolutions()
    {
        using var document = JsonDocument.Parse(
            ProblemAuthoringDefinitionReaderTests.ValidDefinition());
        var root = document.RootElement;
        return JsonSerializer.Serialize(new
        {
            schemaVersion = root.GetProperty("schemaVersion").GetInt32(),
            executionMode = root.GetProperty("executionMode").GetString(),
            functionSignature = root.GetProperty("functionSignature"),
            handwrittenCases = root.GetProperty("handwrittenCases"),
            generator = root.GetProperty("generator"),
            inputValidator = root.GetProperty("inputValidator"),
            referenceSolution = root.GetProperty("referenceSolution"),
            wrongSolutions = new[]
            {
                new { name = "off-by-one", language = "cpp17", source = "wrong-one" },
                new { name = "survivor", language = "cpp17", source = "survivor" }
            }
        });
    }

    private sealed class StubSourceSandbox(IReadOnlyList<SourceGeneratedCase> cases) :
        ISourceGenerationSandbox
    {
        public int InvocationCount { get; private set; }

        public Task<SourceGenerationResult> GenerateAsync(
            SourceGenerationRequest request,
            CancellationToken cancellationToken = default)
        {
            InvocationCount++;
            return Task.FromResult(new SourceGenerationResult(cases, "generator-image:v1"));
        }
    }

    private sealed class AlternatingSourceSandbox : ISourceGenerationSandbox
    {
        private int _invocationCount;

        public Task<SourceGenerationResult> GenerateAsync(
            SourceGenerationRequest request,
            CancellationToken cancellationToken = default)
        {
            _invocationCount++;
            IReadOnlyList<SourceGeneratedCase> cases =
            [
                new SourceGeneratedCase(
                    1,
                    "minimum",
                    "handwritten",
                    0,
                    $"{{\"value\":{_invocationCount}}}")
            ];
            return Task.FromResult(new SourceGenerationResult(cases, "generator-image:v1"));
        }
    }

    private sealed class StubReferenceRunner(IReadOnlyList<string> outputs) :
        IFunctionReferenceSolutionRunner
    {
        public int InvocationCount { get; private set; }

        public Task<IReadOnlyList<string>> RunFunctionAsync(
            string sourceCode,
            FunctionSignature signature,
            IReadOnlyList<string> inputs,
            ReferenceSolutionLimits limits,
            CancellationToken cancellationToken = default)
        {
            InvocationCount++;
            return Task.FromResult(outputs);
        }
    }

    private sealed class AlternatingReferenceRunner : IFunctionReferenceSolutionRunner
    {
        private int _invocationCount;

        public Task<IReadOnlyList<string>> RunFunctionAsync(
            string sourceCode,
            FunctionSignature signature,
            IReadOnlyList<string> inputs,
            ReferenceSolutionLimits limits,
            CancellationToken cancellationToken = default)
        {
            _invocationCount++;
            return Task.FromResult<IReadOnlyList<string>>([_invocationCount.ToString()]);
        }
    }

    private sealed class StubWrongSolutionRunner : IWrongSolutionRunner
    {
        public Task<IReadOnlySet<int>> FindKilledCasesAsync(
            string sourceCode,
            FunctionSignature signature,
            IReadOnlyList<string> inputs,
            IReadOnlyList<string> expectedOutputs,
            ReferenceSolutionLimits limits,
            CancellationToken cancellationToken = default)
        {
            IReadOnlySet<int> killed = sourceCode == "survivor"
                ? new HashSet<int>()
                : new HashSet<int> { 2 };
            return Task.FromResult(killed);
        }
    }
}
