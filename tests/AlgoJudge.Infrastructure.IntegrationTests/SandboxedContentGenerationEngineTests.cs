using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AlgoJudge.Application.ContentGeneration;
using AlgoJudge.Application.FunctionExecution;
using AlgoJudge.Application.Interfaces;
using AlgoJudge.Application.Models.ContentGeneration;
using AlgoJudge.Infrastructure.ContentGeneration;
using Microsoft.Extensions.Configuration;

namespace AlgoJudge.Infrastructure.IntegrationTests;

public sealed class SandboxedContentGenerationEngineTests
{
    [Fact]
    public async Task EngineRepeatsGenerationAndReferenceAndReturnsReviewOnlyStatistics()
    {
        var source = new FakeSourceSandbox();
        var reference = new FakeReferenceRunner();
        var wrong = new FakeWrongRunner();
        var engine = new SandboxedContentGenerationEngine(source, reference, wrong, Configuration());
        var claim = Claim(DefinitionJson());

        var result = await engine.GenerateAsync(claim);

        Assert.Equal(2, source.Calls);
        Assert.Equal(2, reference.Calls);
        Assert.Equal(2, wrong.Calls);
        Assert.Single(result.Cases);
        Assert.Equal(1, result.CasesByGroup["handwritten"]);
        Assert.Equal(["survives"], result.SurvivingWrongSolutions);
        Assert.Equal(1, result.KilledCaseCountByWrongSolution["killed"]);
        Assert.Equal(["killed"], result.Cases[0].KilledWrongSolutions);
        Assert.DoesNotContain(result.SuiteSha256, result.Cases[0].Input, StringComparison.Ordinal);
    }

    private static IConfiguration Configuration() => new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string, string?>
        {
            ["Sandbox:DockerImage"] = "algojudge/judge-cpp17:14.3.0-v1",
            ["DotNetGenerationSandbox:DockerImage"] = "algojudge/generator:10-v1",
            ["ContentGeneration:MaximumCaseCount"] = "500"
        }).Build();

    private static ContentGenerationClaim Claim(string json) => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        "worker", 1, DateTime.UtcNow.AddMinutes(1), json,
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json))), 1000, 262144);

    private static string DefinitionJson() => JsonSerializer.Serialize(new ProblemAuthoringDefinition
    {
        SchemaVersion = 1,
        ExecutionMode = AlgoJudge.Domain.Enums.ProblemExecutionMode.Function,
        FunctionSignature = new FunctionSignature { ClassName = "Solution", MethodName = "solve", ReturnType = FunctionValueType.Int32 },
        HandwrittenCases = [new() { Name = "single", Arguments = JsonSerializer.SerializeToElement(new { }) }],
        Generator = new() { Language = "csharp", SdkVersion = 1, Source = "generator" },
        InputValidator = new() { Language = "csharp", SdkVersion = 1, Source = "validator" },
        ReferenceSolution = new() { Language = "cpp17", Source = "reference" },
        WrongSolutions =
        [
            new() { Name = "survives", Language = "cpp17", Source = "survives" },
            new() { Name = "killed", Language = "cpp17", Source = "killed" }
        ]
    }, new JsonSerializerOptions(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(allowIntegerValues: false) }
    });

    private sealed class FakeSourceSandbox : ISourceGenerationSandbox
    {
        public int Calls { get; private set; }
        public Task<SourceGenerationResult> GenerateAsync(SourceGenerationRequest request, CancellationToken cancellationToken = default)
        { Calls++; return Task.FromResult(new SourceGenerationResult([new SourceGeneratedCase(1, "single", "handwritten", 0, "{}")], "generator-v1")); }
    }
    private sealed class FakeReferenceRunner : IFunctionReferenceSolutionRunner
    {
        public int Calls { get; private set; }
        public Task<IReadOnlyList<string>> RunFunctionAsync(string sourceCode, FunctionSignature signature, IReadOnlyList<string> inputs, ReferenceSolutionLimits limits, CancellationToken cancellationToken = default)
        { Calls++; return Task.FromResult<IReadOnlyList<string>>(["1"]); }
    }
    private sealed class FakeWrongRunner : IWrongSolutionRunner
    {
        public int Calls { get; private set; }
        public Task<IReadOnlySet<int>> FindKilledCasesAsync(string sourceCode, FunctionSignature signature, IReadOnlyList<string> inputs, IReadOnlyList<string> expectedOutputs, ReferenceSolutionLimits limits, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult<IReadOnlySet<int>>(
                sourceCode == "killed" ? new HashSet<int> { 1 } : new HashSet<int>());
        }
    }
}
