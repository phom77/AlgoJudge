using AlgoJudge.Application.ContentGeneration;
using AlgoJudge.Infrastructure.ContentGeneration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace AlgoJudge.ContentTool.Tests;

public sealed class DotNetSourceGenerationSandboxTests
{
    private const string GeneratorSource = """
        using AlgoJudge.ProblemGeneratorSdk;

        public sealed class Generator : ProblemGenerator
        {
            public override void Build(TestPlan plan)
            {
                plan.Random("random-values", 3, context => Args(context.Int(1, 100)));
            }
        }
        """;

    private const string ValidatorSource = """
        using System.Text.Json;
        using AlgoJudge.ProblemGeneratorSdk;

        public sealed class Validator : InputValidator
        {
            public override InputValidationResult Validate(JsonElement arguments) =>
                arguments.GetProperty("value").GetInt32() > 0
                    ? InputValidationResult.Valid
                    : InputValidationResult.Invalid("value must be positive");
        }
        """;

    [DotNetGenerationSandboxFact]
    public async Task SourceIsCompiledAndExecutedDeterministicallyInSandbox()
    {
        var sandbox = CreateSandbox();
        var request = new SourceGenerationRequest(
            GeneratorSource,
            ValidatorSource,
            RootSeed: 123,
            MaximumCaseCount: 10,
            ParameterNames: ["value"],
            HandwrittenCases:
            [
                new SourceHandwrittenCase("minimum", "handwritten", "{\"value\":1}")
            ]);

        var first = await sandbox.GenerateAsync(request);
        var repeated = await sandbox.GenerateAsync(request);

        Assert.Equal(4, first.Cases.Count);
        Assert.Equal(first.ToolchainIdentity, repeated.ToolchainIdentity);
        Assert.Equal(first.Cases, repeated.Cases);
        Assert.All(first.Cases, testCase => Assert.Contains("\"value\":", testCase.Input));
    }

    [DotNetGenerationSandboxFact]
    public async Task InvalidGeneratorSourceFailsDuringSandboxCompilation()
    {
        var sandbox = CreateSandbox();
        var request = new SourceGenerationRequest(
            "this is not valid C#",
            ValidatorSource,
            RootSeed: 123,
            MaximumCaseCount: 10,
            ParameterNames: ["value"],
            HandwrittenCases: []);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sandbox.GenerateAsync(request));

        Assert.Contains("did not compile", exception.Message, StringComparison.Ordinal);
    }

    private static DotNetSourceGenerationSandbox CreateSandbox()
    {
        var image = Environment.GetEnvironmentVariable(
            DotNetGenerationSandboxFactAttribute.ImageEnvironmentVariable)
            ?? throw new InvalidOperationException("Generator test image is not configured.");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DotNetGenerationSandbox:DockerImage"] = image,
                ["DotNetGenerationSandbox:CompileTimeoutSeconds"] = "60",
                ["DotNetGenerationSandbox:RunTimeoutSeconds"] = "30",
                ["DotNetGenerationSandbox:DockerStartupAllowanceSeconds"] = "10",
                ["DotNetGenerationSandbox:MemoryMb"] = "512",
                ["DotNetGenerationSandbox:PidsLimit"] = "64",
                ["DotNetGenerationSandbox:OutputLimitBytes"] = "16777216"
            })
            .Build();
        return new DotNetSourceGenerationSandbox(
            configuration,
            NullLogger<DotNetSourceGenerationSandbox>.Instance);
    }
}
