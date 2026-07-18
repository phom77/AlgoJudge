using AlgoJudge.Application.FunctionExecution;
using AlgoJudge.ContentTool.Configuration;
using AlgoJudge.ContentTool.Packages;
using AlgoJudge.Domain.Enums;
using System.IO.Compression;
using System.Text;

namespace AlgoJudge.ContentTool.Tests;

public sealed class ProblemExecutionModePackageTests
{
    [Fact]
    public async Task SchemaVersionOneDefaultsToStdinStdout()
    {
        using var archive = TestArchive.Create(ValidVersionOneEntries());
        var reader = new ProblemPackageReader(new ContentImportOptions());

        var package = await reader.ReadAsync(archive.Path);

        Assert.Equal(ProblemExecutionMode.StdinStdout, package.Metadata.ExecutionMode);
        Assert.Null(package.Function);
    }

    [Fact]
    public async Task SchemaVersionTwoFunctionPackageIsParsed()
    {
        using var archive = TestArchive.Create(ValidFunctionEntries());
        var reader = new ProblemPackageReader(new ContentImportOptions());

        var package = await reader.ReadAsync(archive.Path);

        Assert.Equal(ProblemExecutionMode.Function, package.Metadata.ExecutionMode);
        var function = Assert.IsType<ProblemPackageFunction>(package.Function);
        Assert.Equal("Solution", function.Signature.ClassName);
        Assert.Equal("twoSum", function.Signature.MethodName);
        Assert.Equal(FunctionValueType.Int32Array, function.Signature.ReturnType);
        Assert.Equal(2, function.Signature.Parameters.Count);
    }

    [Fact]
    public async Task SchemaVersionTwoStdinStdoutPackageDoesNotRequireFunctionFiles()
    {
        var entries = ValidFunctionEntries()
            .Where(entry => !entry.Key.StartsWith("function/", StringComparison.Ordinal))
            .Select(entry => entry.Key == "problem.json"
                ? KeyValuePair.Create(
                    entry.Key,
                    entry.Value.Replace("\"Function\"", "\"StdinStdout\"", StringComparison.Ordinal))
                : entry)
            .ToArray();
        using var archive = TestArchive.Create(entries);
        var reader = new ProblemPackageReader(new ContentImportOptions());

        var package = await reader.ReadAsync(archive.Path);

        Assert.Equal(ProblemExecutionMode.StdinStdout, package.Metadata.ExecutionMode);
        Assert.Null(package.Function);
    }

    [Fact]
    public async Task SchemaVersionTwoRequiresExecutionMode()
    {
        var entries = ValidFunctionEntries()
            .Select(entry => entry.Key == "problem.json"
                ? KeyValuePair.Create(
                    entry.Key,
                    entry.Value
                        .Replace(
                            "  \"executionMode\": \"Function\",\r\n",
                            string.Empty,
                            StringComparison.Ordinal)
                        .Replace(
                            "  \"executionMode\": \"Function\",\n",
                            string.Empty,
                            StringComparison.Ordinal))
                : entry)
            .ToArray();
        using var archive = TestArchive.Create(entries);
        var reader = new ProblemPackageReader(new ContentImportOptions());

        var exception = await Assert.ThrowsAsync<PackageValidationException>(() =>
            reader.ReadAsync(archive.Path));

        Assert.Contains(
            "problem.json schema version 2 requires executionMode.",
            exception.Errors);
    }

    [Fact]
    public async Task SchemaVersionOneRejectsFunctionFiles()
    {
        var entries = ValidVersionOneEntries()
            .Concat(ValidFunctionEntries().Where(entry => entry.Key.StartsWith(
                "function/",
                StringComparison.Ordinal)))
            .ToArray();
        using var archive = TestArchive.Create(entries);
        var reader = new ProblemPackageReader(new ContentImportOptions());

        var exception = await Assert.ThrowsAsync<PackageValidationException>(() =>
            reader.ReadAsync(archive.Path));

        Assert.Contains(
            "Schema-version-1 packages cannot contain function files.",
            exception.Errors);
    }

    [Fact]
    public async Task FunctionModeRequiresBothFunctionFiles()
    {
        var entries = ValidFunctionEntries()
            .Where(entry => entry.Key != "function/adapter-template.cpp")
            .ToArray();
        using var archive = TestArchive.Create(entries);
        var reader = new ProblemPackageReader(new ContentImportOptions());

        var exception = await Assert.ThrowsAsync<PackageValidationException>(() =>
            reader.ReadAsync(archive.Path));

        Assert.Contains(
            "Required archive entry is missing: function/adapter-template.cpp.",
            exception.Errors);
    }

    [Theory]
    [InlineData("samples/01.in", "{\"nums\":[2,7],\"extra\":9}", "unknown argument extra")]
    [InlineData("samples/01.in", "{\"nums\":[2,7]}", "missing argument target")]
    [InlineData("samples/01.out", "\"not-an-array\"", "does not match returnType")]
    public async Task FunctionCasesMustMatchSignature(
        string entryName,
        string replacement,
        string expectedError)
    {
        var entries = ValidFunctionEntries()
            .Select(entry => entry.Key == entryName
                ? KeyValuePair.Create(entry.Key, replacement)
                : entry)
            .ToArray();
        using var archive = TestArchive.Create(entries);
        var reader = new ProblemPackageReader(new ContentImportOptions());

        var exception = await Assert.ThrowsAsync<PackageValidationException>(() =>
            reader.ReadAsync(archive.Path));

        Assert.Contains(
            exception.Errors,
            error => error.Contains(expectedError, StringComparison.Ordinal));
    }

    [Fact]
    public async Task FunctionAdapterRejectsUnknownOrDuplicatePlaceholders()
    {
        var entries = ValidFunctionEntries()
            .Select(entry => entry.Key == "function/adapter-template.cpp"
                ? KeyValuePair.Create(
                    entry.Key,
                    entry.Value + "\n{{USER_SOURCE}}\n{{UNSUPPORTED}}")
                : entry)
            .ToArray();
        using var archive = TestArchive.Create(entries);
        var reader = new ProblemPackageReader(new ContentImportOptions());

        var exception = await Assert.ThrowsAsync<PackageValidationException>(() =>
            reader.ReadAsync(archive.Path));

        Assert.Contains(
            exception.Errors,
            error => error.Contains("exactly one {{USER_SOURCE}}", StringComparison.Ordinal));
        Assert.Contains(
            "Unknown function adapter placeholder: {{UNSUPPORTED}}.",
            exception.Errors);
    }

    private static IReadOnlyCollection<KeyValuePair<string, string>> ValidVersionOneEntries() =>
        new Dictionary<string, string>
        {
            ["problem.json"] = """
                {
                  "schemaVersion": 1,
                  "slug": "two-sum",
                  "title": "Two Sum",
                  "difficulty": "Easy",
                  "timeLimitMs": 1000,
                  "memoryLimitKb": 262144,
                  "tags": []
                }
                """,
            ["statement.md"] = "# Two Sum",
            ["constraints.md"] = "- Valid input.",
            ["samples/01.in"] = "sample input",
            ["samples/01.out"] = "sample output",
            ["tests/001.in"] = "hidden input",
            ["tests/001.out"] = "hidden output"
        };

    private static IReadOnlyCollection<KeyValuePair<string, string>> ValidFunctionEntries() =>
        new Dictionary<string, string>
        {
            ["problem.json"] = """
                {
                  "schemaVersion": 2,
                  "executionMode": "Function",
                  "slug": "two-sum",
                  "title": "Two Sum",
                  "difficulty": "Easy",
                  "timeLimitMs": 1000,
                  "memoryLimitKb": 262144,
                  "tags": []
                }
                """,
            ["statement.md"] = "# Two Sum",
            ["constraints.md"] = "- Valid arguments.",
            ["samples/01.in"] = "{\"nums\":[2,7,11,15],\"target\":9}",
            ["samples/01.out"] = "[0,1]",
            ["tests/001.in"] = "{\"nums\":[3,2,4],\"target\":6}",
            ["tests/001.out"] = "[1,2]",
            ["function/signature.json"] = """
                {
                  "className": "Solution",
                  "methodName": "twoSum",
                  "returnType": "Int32Array",
                  "parameters": [
                    { "name": "nums", "type": "Int32Array" },
                    { "name": "target", "type": "Int32" }
                  ]
                }
                """,
            ["function/adapter-template.cpp"] = """
                #include <iostream>
                {{USER_SOURCE}}
                int main() {
                    {{CLASS_NAME}} solution;
                    // The problem adapter parses normalized JSON and invokes the declared method.
                    return 0; // {{METHOD_NAME}}
                }
                """
        };

    private sealed class TestArchive : IDisposable
    {
        private TestArchive(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TestArchive Create(
            IEnumerable<KeyValuePair<string, string>> entries)
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"algojudge-execution-mode-{Guid.NewGuid():N}.zip");
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
            foreach (var (name, content) in entries)
            {
                var entry = archive.CreateEntry(name, CompressionLevel.Fastest);
                using var entryStream = entry.Open();
                using var writer = new StreamWriter(
                    entryStream,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                writer.Write(content);
            }

            return new TestArchive(path);
        }

        public void Dispose()
        {
            if (File.Exists(Path))
                File.Delete(Path);
        }
    }
}
