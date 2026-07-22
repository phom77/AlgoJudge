using System.Text.Json;
using AlgoJudge.ContentTool.Configuration;
using AlgoJudge.ContentTool.Generation;

namespace AlgoJudge.ContentTool.Tests;

public sealed class ProblemAuthoringDefinitionReaderTests
{
    [Fact]
    public async Task ValidSourceDefinitionIsReadAndHashed()
    {
        using var directory = AuthoringDirectory.Create(ValidDefinition());
        var reader = new ProblemAuthoringDefinitionReader(new ContentImportOptions());

        var document = await reader.ReadAsync(directory.Path);

        Assert.Equal(1, document.Definition.SchemaVersion);
        Assert.Equal("solve", document.Definition.FunctionSignature.MethodName);
        Assert.Single(document.Definition.HandwrittenCases);
        Assert.Equal(64, document.DefinitionSha256.Length);
    }

    [Fact]
    public async Task DuplicatePropertiesAreRejected()
    {
        using var directory = AuthoringDirectory.Create(
            ValidDefinition().Replace(
                "\"schemaVersion\":1",
                "\"schemaVersion\":1,\"schemaVersion\":1",
                StringComparison.Ordinal));

        var exception = await Assert.ThrowsAsync<TestGenerationException>(() =>
            new ProblemAuthoringDefinitionReader(new ContentImportOptions())
                .ReadAsync(directory.Path));

        Assert.Contains("invalid", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandwrittenArgumentsMustMatchSignature()
    {
        using var directory = AuthoringDirectory.Create(
            ValidDefinition().Replace("\"arguments\":{\"value\":1}", "\"arguments\":{\"other\":1}", StringComparison.Ordinal));

        var exception = await Assert.ThrowsAsync<TestGenerationException>(() =>
            new ProblemAuthoringDefinitionReader(new ContentImportOptions())
                .ReadAsync(directory.Path));

        Assert.Contains("missing argument value", exception.Message, StringComparison.Ordinal);
    }

    internal static string ValidDefinition() =>
        JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            executionMode = "Function",
            functionSignature = new
            {
                className = "Solution",
                methodName = "solve",
                returnType = "Int32",
                parameters = new[] { new { name = "value", type = "Int32" } }
            },
            handwrittenCases = new[]
            {
                new { name = "minimum", group = "handwritten", arguments = new { value = 1 } }
            },
            generator = new
            {
                language = "csharp",
                sdkVersion = 1,
                source = "public sealed class Generator : ProblemGenerator { public override void Build(TestPlan plan) {} }"
            },
            inputValidator = new
            {
                language = "csharp",
                sdkVersion = 1,
                source = "public sealed class Validator : InputValidator { public override InputValidationResult Validate(JsonElement value) => InputValidationResult.Valid; }"
            },
            referenceSolution = new
            {
                language = "cpp17",
                source = "class Solution { public: int solve(int value) { return value; } };"
            },
            wrongSolutions = Array.Empty<object>()
        });

    internal sealed class AuthoringDirectory : IDisposable
    {
        private AuthoringDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static AuthoringDirectory Create(string definition)
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"algojudge-source-authoring-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            File.WriteAllText(System.IO.Path.Combine(path, "authoring.json"), definition);
            File.WriteAllText(
                System.IO.Path.Combine(path, "problem.json"),
                "{\"timeLimitMs\":1000,\"memoryLimitKb\":262144}");
            return new AuthoringDirectory(path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
