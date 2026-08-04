using System.Text.Json;
using AlgoJudge.Application.ContentGeneration;
using AlgoJudge.Application.Interfaces;
using AlgoJudge.Application.Models.ContentGeneration;
using AlgoJudge.ContentTool.Configuration;
using AlgoJudge.ContentTool.Workspace;
using AlgoJudge.Domain.Execution;
using AlgoJudge.Infrastructure.ContentGeneration;
using AlgoJudge.Infrastructure.Grading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace AlgoJudge.ContentTool.Tests;

public sealed class PracticeCatalogPipelineTests
{
    [PracticeCatalogPipelineFact]
    public async Task EveryProblemGeneratesAQualityApprovedSuite()
    {
        var configuration = Configuration();
        var sourceSandbox = new DotNetSourceGenerationSandbox(
            configuration,
            NullLogger<DotNetSourceGenerationSandbox>.Instance);
        IDockerSandbox cppSandbox = new DockerSandboxService(
            configuration,
            NullLogger<DockerSandboxService>.Instance);
        var engine = new SandboxedContentGenerationEngine(
            sourceSandbox,
            new Cpp17ContentReferenceRunner(
                cppSandbox,
                new Cpp17FunctionHarnessBuilder()),
            new Cpp17ContentWrongSolutionRunner(
                cppSandbox,
                new Cpp17FunctionHarnessBuilder(),
                new OutputChecker()),
            configuration);
        var catalogPath = Path.Combine(
            FindRepositoryRoot(),
            "content",
            "practice-catalog",
            "catalog.json");
        var resolution = await new ContentWorkspaceResolver(new ContentImportOptions())
            .ResolveAsync(catalogPath);

        Assert.Equal(10, resolution.Problems.Count);
        var outputChecker = new OutputChecker();
        foreach (var problem in resolution.Problems)
        {
            var definitionJson = ProblemAuthoringDefinitionJson.Serialize(
                problem.Definition);
            var result = await engine.GenerateAsync(new ContentGenerationClaim(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                "practice-catalog-test",
                1,
                DateTime.UtcNow.AddMinutes(5),
                definitionJson,
                ProblemAuthoringDefinitionJson.ComputeSha256(problem.Definition),
                problem.Metadata.TimeLimitMs,
                problem.Metadata.MemoryLimitKb));

            Assert.Equal(502, result.Cases.Count);
            Assert.Equal(1, result.WrongSolutionCount);
            Assert.Empty(result.SurvivingWrongSolutions);
            Assert.True(
                result.KilledCaseCountByWrongSolution.Values.Single() > 0,
                problem.Metadata.Slug);
            Assert.Equal(2, result.CasesByGroup["handwritten"]);
            Assert.Equal(20, result.CasesByGroup["edge"]);
            Assert.Equal(450, result.CasesByGroup["random"]);
            Assert.Equal(30, result.CasesByGroup["adversarial"]);
            for (var sampleIndex = 0;
                 sampleIndex < problem.Metadata.Samples.Count;
                 sampleIndex++)
            {
                Assert.True(
                    outputChecker.IsMatch(
                        OutputCheckerConfiguration.JsonExact,
                        problem.Metadata.Samples[sampleIndex].Expected.GetRawText(),
                        result.Cases[sampleIndex].ExpectedOutput),
                    $"{problem.Metadata.Slug} sample {sampleIndex + 1}");
            }
            Assert.All(result.Cases, testCase =>
            {
                using var input = JsonDocument.Parse(testCase.Input);
                using var output = JsonDocument.Parse(testCase.ExpectedOutput);
                Assert.Equal(JsonValueKind.Object, input.RootElement.ValueKind);
                Assert.NotEqual(JsonValueKind.Undefined, output.RootElement.ValueKind);
            });
        }
    }

    private static IConfiguration Configuration() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DotNetGenerationSandbox:DockerImage"] = Environment.GetEnvironmentVariable(
                PracticeCatalogPipelineFactAttribute.GeneratorImageVariable),
            ["DotNetGenerationSandbox:CompileTimeoutSeconds"] = "60",
            ["DotNetGenerationSandbox:RunTimeoutSeconds"] = "30",
            ["DotNetGenerationSandbox:DockerStartupAllowanceSeconds"] = "10",
            ["DotNetGenerationSandbox:MemoryMb"] = "512",
            ["DotNetGenerationSandbox:PidsLimit"] = "64",
            ["DotNetGenerationSandbox:OutputLimitBytes"] = "16777216",
            ["Sandbox:DockerImage"] = Environment.GetEnvironmentVariable(
                PracticeCatalogPipelineFactAttribute.JudgeImageVariable),
            ["Sandbox:CompileTimeoutSeconds"] = "30",
            ["Sandbox:DockerStartupAllowanceSeconds"] = "10",
            ["Sandbox:StdoutLimitBytes"] = "65536",
            ["Sandbox:StderrLimitBytes"] = "65536",
            ["Sandbox:PidsLimit"] = "32",
            ["ContentGeneration:MaximumCaseCount"] = "1000"
        }).Build();

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AlgoJudge.slnx")))
                return directory.FullName;
        }
        throw new InvalidOperationException("Could not locate the repository root.");
    }
}
