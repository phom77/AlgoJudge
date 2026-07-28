using System.Text.Json;
using AlgoJudge.ContentTool.Configuration;
using AlgoJudge.ContentTool.Workspace;

namespace AlgoJudge.ContentTool.Tests;

public sealed class ContentWorkspaceResolverTests
{
    [Fact]
    public async Task ResolvesTemplateDefaultsIntoCanonicalDefinition()
    {
        using var workspace = WorkspaceFixture.Create();

        var result = await CreateResolver().ResolveAsync(workspace.CatalogPath);

        var problem = Assert.Single(result.Problems);
        Assert.Equal("maximum-subarray", problem.Metadata.Slug);
        Assert.Equal(1_500, problem.Metadata.TimeLimitMs);
        Assert.Equal(131_072, problem.Metadata.MemoryLimitKb);
        Assert.Equal("cpp17", problem.Metadata.Language);
        Assert.Contains("TemplateGenerator", problem.Definition.Generator.Source);
        Assert.Contains("TemplateValidator", problem.Definition.InputValidator.Source);
        Assert.Contains("ReferenceSolution", problem.Definition.ReferenceSolution.Source);
        Assert.Equal(1, problem.Definition.Generator.SdkVersion);
        Assert.Single(problem.Definition.HandwrittenCases);
        Assert.Equal(500, problem.GeneratorParameters.GetProperty("caseCount").GetInt32());
        Assert.Equal("templates/int-array-function/generator.cs", problem.SourceOrigins.Generator);
        Assert.Equal(64, problem.ContentHash.Length);
    }

    [Fact]
    public async Task ProblemSourcesOverrideTemplateAndWrongSolutionsAreDiscovered()
    {
        using var workspace = WorkspaceFixture.Create();
        workspace.WriteProblemFile(
            "generator.cs",
            "public sealed class ProblemGeneratorOverride { }");
        workspace.WriteProblemFile(
            "validator.cs",
            "public sealed class ProblemValidatorOverride { }");
        workspace.WriteProblemFile(
            "wrong-solutions/returns-zero.cpp",
            "class Solution { public: int solve(int value) { return 0; } };");

        var problem = Assert.Single(
            (await CreateResolver().ResolveAsync(workspace.CatalogPath)).Problems);

        Assert.Contains("ProblemGeneratorOverride", problem.Definition.Generator.Source);
        Assert.Contains("ProblemValidatorOverride", problem.Definition.InputValidator.Source);
        var wrongSolution = Assert.Single(problem.Definition.WrongSolutions);
        Assert.Equal("returns-zero", wrongSolution.Name);
        Assert.Equal("problems/maximum-subarray/generator.cs", problem.SourceOrigins.Generator);
        Assert.Equal(
            "problems/maximum-subarray/wrong-solutions/returns-zero.cpp",
            Assert.Single(problem.SourceOrigins.WrongSolutions));
    }

    [Fact]
    public async Task ProblemValuesOverrideTemplateValues()
    {
        using var workspace = WorkspaceFixture.Create();
        workspace.WriteProblemJson(
            WorkspaceFixture.ValidProblemJson()
                .Replace(
                    "\"generatorParameters\":",
                    "\"timeLimitMs\":2300,\"memoryLimitKb\":196608," +
                    "\"qualityPolicy\":{\"minimumTestCaseCount\":9," +
                    "\"minimumCasesByGroup\":[{\"group\":\"handwritten\",\"minimumCaseCount\":1}]," +
                    "\"requireEachDeclaredWrongSolutionKilled\":false}," +
                    "\"generatorParameters\":",
                    StringComparison.Ordinal));

        var problem = Assert.Single(
            (await CreateResolver().ResolveAsync(workspace.CatalogPath)).Problems);

        Assert.Equal(2_300, problem.Metadata.TimeLimitMs);
        Assert.Equal(196_608, problem.Metadata.MemoryLimitKb);
        Assert.Equal(9, problem.Definition.QualityPolicy.MinimumTestCaseCount);
        Assert.False(problem.Definition.QualityPolicy.RequireEachDeclaredWrongSolutionKilled);
    }

    [Fact]
    public async Task EffectiveHashIsStableAcrossGeneratorParameterPropertyOrder()
    {
        using var first = WorkspaceFixture.Create();
        using var second = WorkspaceFixture.Create();
        second.WriteProblemJson(
            WorkspaceFixture.ValidProblemJson().Replace(
                "\"minimumLength\":1,\"caseCount\":500",
                "\"caseCount\":500,\"minimumLength\":1",
                StringComparison.Ordinal));

        var firstHash = Assert.Single(
            (await CreateResolver().ResolveAsync(first.CatalogPath)).Problems).ContentHash;
        var secondHash = Assert.Single(
            (await CreateResolver().ResolveAsync(second.CatalogPath)).Problems).ContentHash;

        Assert.Equal(firstHash, secondHash);
    }

    [Fact]
    public async Task EffectiveHashChangesWhenEffectiveSourceChanges()
    {
        using var workspace = WorkspaceFixture.Create();
        var original = Assert.Single(
            (await CreateResolver().ResolveAsync(workspace.CatalogPath)).Problems).ContentHash;
        workspace.WriteProblemFile(
            "reference.cpp",
            "class ReferenceSolution { public: int solve(int value) { return value + 1; } };");

        var changed = Assert.Single(
            (await CreateResolver().ResolveAsync(workspace.CatalogPath)).Problems).ContentHash;

        Assert.NotEqual(original, changed);
    }

    [Fact]
    public async Task DuplicateEnabledProblemSlugsAreRejected()
    {
        using var workspace = WorkspaceFixture.Create();
        workspace.CopyProblem("second");
        workspace.WriteCatalog(
            """
            {
              "schemaVersion": 1,
              "problems": [
                { "path": "problems/maximum-subarray", "action": "create", "enabled": true },
                { "path": "problems/second", "action": "new-revision", "enabled": true }
              ]
            }
            """);

        var exception = await Assert.ThrowsAsync<WorkspaceValidationException>(
            () => CreateResolver().ResolveAsync(workspace.CatalogPath));

        Assert.Contains(
            exception.Errors,
            error => error.Contains("Duplicate problem slug", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("catalog")]
    [InlineData("problem")]
    [InlineData("template")]
    public async Task UnknownJsonFieldsAreRejected(string document)
    {
        using var workspace = WorkspaceFixture.Create();
        if (document == "catalog")
        {
            workspace.WriteCatalog(
                WorkspaceFixture.ValidCatalogJson().Replace(
                    "\"schemaVersion\": 1,",
                    "\"schemaVersion\": 1, \"unknown\": true,",
                    StringComparison.Ordinal));
        }
        else if (document == "problem")
        {
            workspace.WriteProblemJson(
                WorkspaceFixture.ValidProblemJson().Replace(
                    "\"schemaVersion\":1,",
                    "\"schemaVersion\":1,\"unknown\":true,",
                    StringComparison.Ordinal));
        }
        else
        {
            workspace.WriteTemplateJson(
                WorkspaceFixture.ValidTemplateJson().Replace(
                    "\"schemaVersion\":1,",
                    "\"schemaVersion\":1,\"unknown\":true,",
                    StringComparison.Ordinal));
        }

        var exception = await Assert.ThrowsAsync<WorkspaceValidationException>(
            () => CreateResolver().ResolveAsync(workspace.CatalogPath));

        Assert.NotEmpty(exception.Errors);
    }

    [Fact]
    public async Task DuplicateJsonPropertiesAreRejected()
    {
        using var workspace = WorkspaceFixture.Create();
        workspace.WriteProblemJson(
            WorkspaceFixture.ValidProblemJson().Replace(
                "\"schemaVersion\":1",
                "\"schemaVersion\":1,\"schemaVersion\":1",
                StringComparison.Ordinal));

        var exception = await Assert.ThrowsAsync<WorkspaceValidationException>(
            () => CreateResolver().ResolveAsync(workspace.CatalogPath));

        Assert.Contains(
            exception.Errors,
            error => error.Contains("Duplicate JSON property", StringComparison.Ordinal));
    }

    [Fact]
    public async Task UnknownNestedJsonFieldsAreRejected()
    {
        using var workspace = WorkspaceFixture.Create();
        workspace.WriteProblemJson(
            WorkspaceFixture.ValidProblemJson().Replace(
                "\"methodName\":\"solve\"",
                "\"methodName\":\"solve\",\"unknown\":true",
                StringComparison.Ordinal));

        var exception = await Assert.ThrowsAsync<WorkspaceValidationException>(
            () => CreateResolver().ResolveAsync(workspace.CatalogPath));

        Assert.NotEmpty(exception.Errors);
    }

    [Fact]
    public async Task MissingTemplateIsRejected()
    {
        using var workspace = WorkspaceFixture.Create();
        workspace.WriteProblemJson(
            WorkspaceFixture.ValidProblemJson().Replace(
                "\"template\":\"int-array-function\"",
                "\"template\":\"does-not-exist\"",
                StringComparison.Ordinal));

        var exception = await Assert.ThrowsAsync<WorkspaceValidationException>(
            () => CreateResolver().ResolveAsync(workspace.CatalogPath));

        Assert.Contains(
            exception.Errors,
            error => error.Contains("does not exist", StringComparison.Ordinal));
    }

    [Fact]
    public async Task MissingReferenceSolutionIsRejected()
    {
        using var workspace = WorkspaceFixture.Create();
        File.Delete(Path.Combine(workspace.ProblemDirectory, "reference.cpp"));

        var exception = await Assert.ThrowsAsync<WorkspaceValidationException>(
            () => CreateResolver().ResolveAsync(workspace.CatalogPath));

        Assert.Contains(
            exception.Errors,
            error => error.Contains("reference.cpp", StringComparison.Ordinal));
    }

    [Fact]
    public async Task MissingTemplateGeneratorIsRejectedEvenWhenValidatorExists()
    {
        using var workspace = WorkspaceFixture.Create();
        File.Delete(Path.Combine(
            workspace.Root,
            "templates",
            "int-array-function",
            "generator.cs"));

        var exception = await Assert.ThrowsAsync<WorkspaceValidationException>(
            () => CreateResolver().ResolveAsync(workspace.CatalogPath));

        Assert.Contains(
            exception.Errors,
            error => error.Contains("generator.cs", StringComparison.Ordinal));
    }

    [Fact]
    public async Task MissingRequiredProblemMetadataIsRejected()
    {
        using var workspace = WorkspaceFixture.Create();
        using var document = JsonDocument.Parse(WorkspaceFixture.ValidProblemJson());
        var withoutTitle = document.RootElement
            .EnumerateObject()
            .Where(property => property.Name != "title")
            .ToDictionary(
                property => property.Name,
                property => property.Value.Clone(),
                StringComparer.Ordinal);
        workspace.WriteProblemJson(JsonSerializer.Serialize(withoutTitle));

        var exception = await Assert.ThrowsAsync<WorkspaceValidationException>(
            () => CreateResolver().ResolveAsync(workspace.CatalogPath));

        Assert.Contains(
            exception.Errors,
            error => error.Contains("requires property title", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("C:/outside")]
    [InlineData("problems\\maximum-subarray")]
    public async Task UnsafeCatalogPathsAreRejected(string path)
    {
        using var workspace = WorkspaceFixture.Create();
        workspace.WriteCatalog(
            $$"""
            {
              "schemaVersion": 1,
              "problems": [
                { "path": {{JsonSerializer.Serialize(path)}}, "action": "create", "enabled": true }
              ]
            }
            """);

        var exception = await Assert.ThrowsAsync<WorkspaceValidationException>(
            () => CreateResolver().ResolveAsync(workspace.CatalogPath));

        Assert.Contains(
            exception.Errors,
            error => error.Contains("relative path", StringComparison.Ordinal) ||
                     error.Contains("unsafe", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SymbolicLinkEscapeIsRejectedWhenHostSupportsLinks()
    {
        using var workspace = WorkspaceFixture.Create();
        var outside = Path.Combine(
            Path.GetTempPath(),
            $"algojudge-workspace-outside-{Guid.NewGuid():N}.cpp");
        await File.WriteAllTextAsync(outside, "class Escaped { };");
        var reference = Path.Combine(workspace.ProblemDirectory, "reference.cpp");
        File.Delete(reference);
        try
        {
            try
            {
                File.CreateSymbolicLink(reference, outside);
            }
            catch (Exception exception) when (
                exception is UnauthorizedAccessException or
                PlatformNotSupportedException or
                IOException)
            {
                return;
            }

            var validation = await Assert.ThrowsAsync<WorkspaceValidationException>(
                () => CreateResolver().ResolveAsync(workspace.CatalogPath));
            Assert.Contains(
                validation.Errors,
                error => error.Contains("symbolic link", StringComparison.Ordinal));
        }
        finally
        {
            if (File.Exists(reference))
                File.Delete(reference);
            if (File.Exists(outside))
                File.Delete(outside);
        }
    }

    [Theory]
    [InlineData("\"minimumLength\":0,\"caseCount\":500", "less than minimum")]
    [InlineData("\"minimumLength\":1,\"caseCount\":500,\"extra\":1", "unknown generator parameter")]
    public async Task InvalidGeneratorParametersAreRejected(
        string generatorParameters,
        string expectedError)
    {
        using var workspace = WorkspaceFixture.Create();
        workspace.WriteProblemJson(
            WorkspaceFixture.ValidProblemJson().Replace(
                "\"minimumLength\":1,\"caseCount\":500",
                generatorParameters,
                StringComparison.Ordinal));

        var exception = await Assert.ThrowsAsync<WorkspaceValidationException>(
            () => CreateResolver().ResolveAsync(workspace.CatalogPath));

        Assert.Contains(
            exception.Errors,
            error => error.Contains(expectedError, StringComparison.Ordinal));
    }

    [Fact]
    public async Task GeneratorParameterDefaultsAreMaterialized()
    {
        using var workspace = WorkspaceFixture.Create();
        workspace.WriteProblemJson(
            WorkspaceFixture.ValidProblemJson().Replace(
                "\"minimumLength\":1,\"caseCount\":500",
                "\"minimumLength\":1",
                StringComparison.Ordinal));

        var problem = Assert.Single(
            (await CreateResolver().ResolveAsync(workspace.CatalogPath)).Problems);

        Assert.Equal(100, problem.GeneratorParameters.GetProperty("caseCount").GetInt32());
    }

    [Fact]
    public async Task MissingRequiredGeneratorParameterIsRejected()
    {
        using var workspace = WorkspaceFixture.Create();
        workspace.WriteProblemJson(
            WorkspaceFixture.ValidProblemJson().Replace(
                "\"minimumLength\":1,\"caseCount\":500",
                "\"caseCount\":500",
                StringComparison.Ordinal));

        var exception = await Assert.ThrowsAsync<WorkspaceValidationException>(
            () => CreateResolver().ResolveAsync(workspace.CatalogPath));

        Assert.Contains(
            exception.Errors,
            error => error.Contains(
                "required generator parameter is missing: minimumLength",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task UnknownGeneratorSchemaKeywordIsRejected()
    {
        using var workspace = WorkspaceFixture.Create();
        workspace.WriteTemplateJson(
            WorkspaceFixture.ValidTemplateJson().Replace(
                "\"minimum\":1,\"maximum\":100000",
                "\"minimum\":1,\"maximum\":100000,\"pattern\":\".*\"",
                StringComparison.Ordinal));

        var exception = await Assert.ThrowsAsync<WorkspaceValidationException>(
            () => CreateResolver().ResolveAsync(workspace.CatalogPath));

        Assert.Contains(
            exception.Errors,
            error => error.Contains("unknown keyword pattern", StringComparison.Ordinal));
    }

    private static ContentWorkspaceResolver CreateResolver() =>
        new(new ContentImportOptions());

    private sealed class WorkspaceFixture : IDisposable
    {
        private WorkspaceFixture(string root)
        {
            Root = root;
        }

        public string Root { get; }
        public string CatalogPath => Path.Combine(Root, "catalog.json");
        public string ProblemDirectory => Path.Combine(Root, "problems", "maximum-subarray");

        public static WorkspaceFixture Create()
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                $"algojudge-content-workspace-{Guid.NewGuid():N}");
            var fixture = new WorkspaceFixture(root);
            Directory.CreateDirectory(
                Path.Combine(root, "templates", "int-array-function"));
            Directory.CreateDirectory(fixture.ProblemDirectory);
            fixture.WriteCatalog(ValidCatalogJson());
            fixture.WriteTemplateJson(ValidTemplateJson());
            fixture.WriteProblemJson(ValidProblemJson());
            fixture.WriteTemplateFile(
                "generator.cs",
                "public sealed class TemplateGenerator { }");
            fixture.WriteTemplateFile(
                "validator.cs",
                "public sealed class TemplateValidator { }");
            fixture.WriteProblemFile(
                "reference.cpp",
                "class ReferenceSolution { public: int solve(int value) { return value; } };");
            return fixture;
        }

        public static string ValidCatalogJson() =>
            """
            {
              "schemaVersion": 1,
              "problems": [
                {
                  "path": "problems/maximum-subarray",
                  "action": "create",
                  "enabled": true
                }
              ]
            }
            """;

        public static string ValidTemplateJson() =>
            """
            {
              "schemaVersion":1,
              "executionMode":"Function",
              "language":"cpp17",
              "generatorSdkVersion":1,
              "timeLimitMs":1500,
              "memoryLimitKb":131072,
              "qualityPolicy":{
                "minimumTestCaseCount":2,
                "minimumCasesByGroup":[
                  {"group":"handwritten","minimumCaseCount":1}
                ],
                "requireEachDeclaredWrongSolutionKilled":true
              },
              "generatorParametersSchema":{
                "type":"object",
                "properties":{
                  "minimumLength":{"type":"integer","minimum":1,"maximum":100000},
                  "caseCount":{"type":"integer","minimum":1,"maximum":1000,"default":100}
                },
                "required":["minimumLength"],
                "additionalProperties":false
              }
            }
            """;

        public static string ValidProblemJson() =>
            """
            {
              "schemaVersion":1,
              "template":"int-array-function",
              "slug":"maximum-subarray",
              "title":"Maximum Subarray",
              "difficulty":"Medium",
              "tags":["array","dynamic-programming"],
              "statement":"Return the input value.",
              "constraints":["1 <= value <= 100000"],
              "signature":{
                "className":"Solution",
                "methodName":"solve",
                "returnType":"Int32",
                "parameters":[{"name":"value","type":"Int32"}]
              },
              "samples":[
                {
                  "arguments":{"value":7},
                  "expected":7,
                  "explanation":"The value is returned."
                }
              ],
              "generatorParameters":{"minimumLength":1,"caseCount":500}
            }
            """;

        public void WriteCatalog(string content) =>
            File.WriteAllText(CatalogPath, content);

        public void WriteProblemJson(string content) =>
            WriteProblemFile("problem.json", content);

        public void WriteTemplateJson(string content) =>
            WriteTemplateFile("template.json", content);

        public void WriteProblemFile(string relativePath, string content) =>
            WriteFile(ProblemDirectory, relativePath, content);

        public void WriteTemplateFile(string relativePath, string content) =>
            WriteFile(
                Path.Combine(Root, "templates", "int-array-function"),
                relativePath,
                content);

        public void CopyProblem(string name)
        {
            var target = Path.Combine(Root, "problems", name);
            Directory.CreateDirectory(target);
            foreach (var file in Directory.EnumerateFiles(
                         ProblemDirectory,
                         "*",
                         SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(ProblemDirectory, file);
                var destination = Path.Combine(target, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(file, destination);
            }
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }

        private static void WriteFile(string root, string relativePath, string content)
        {
            var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }
    }
}
