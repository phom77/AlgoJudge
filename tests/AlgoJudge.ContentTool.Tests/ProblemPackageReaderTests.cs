using AlgoJudge.ContentTool.Configuration;
using AlgoJudge.ContentTool.Packages;
using AlgoJudge.Domain.Enums;
using System.IO.Compression;
using System.Text;

namespace AlgoJudge.ContentTool.Tests;

public class ProblemPackageReaderTests
{
    [Fact]
    public async Task ValidPackageIsParsedWithoutDatabaseAccess()
    {
        using var archive = TestArchive.Create(ValidEntries());
        var reader = new ProblemPackageReader(new ContentImportOptions());

        var package = await reader.ReadAsync(archive.Path);

        Assert.Equal("two-sum", package.Metadata.Slug);
        Assert.Equal(DifficultyLevel.Easy, package.Metadata.Difficulty);
        Assert.Equal(2, package.Metadata.Tags.Count);
        Assert.Equal("Example explanation", Assert.Single(package.Samples).Explanation);
        Assert.Equal(2, package.JudgeTestCases.Count);
        Assert.Equal(new[] { 1, 2 }, package.JudgeTestCases.Select(testCase => testCase.Ordinal));
    }

    [Fact]
    public async Task MissingInputOutputPairIsRejected()
    {
        var entries = ValidEntries()
            .Where(entry => entry.Key != "tests/001.out")
            .ToArray();
        using var archive = TestArchive.Create(entries);
        var reader = new ProblemPackageReader(new ContentImportOptions());

        var exception = await Assert.ThrowsAsync<PackageValidationException>(() =>
            reader.ReadAsync(archive.Path));

        Assert.Contains(
            exception.Errors,
            error => error.Contains("missing its .out file", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CaseInsensitiveDuplicateEntryNamesAreRejected()
    {
        var entries = ValidEntries()
            .Append(new KeyValuePair<string, string>("Statement.md", "Duplicate"))
            .ToArray();
        using var archive = TestArchive.Create(entries);
        var reader = new ProblemPackageReader(new ContentImportOptions());

        var exception = await Assert.ThrowsAsync<PackageValidationException>(() =>
            reader.ReadAsync(archive.Path));

        Assert.Contains(
            exception.Errors,
            error => error.Contains("Duplicate archive entry name", StringComparison.Ordinal));
    }

    [Fact]
    public async Task UnsafeArchivePathIsRejected()
    {
        var entries = ValidEntries()
            .Append(new KeyValuePair<string, string>("../escape.txt", "unsafe"))
            .ToArray();
        using var archive = TestArchive.Create(entries);
        var reader = new ProblemPackageReader(new ContentImportOptions());

        var exception = await Assert.ThrowsAsync<PackageValidationException>(() =>
            reader.ReadAsync(archive.Path));

        Assert.Contains("The archive contains an unsafe entry path.", exception.Errors);
    }

    [Fact]
    public async Task EntrySizeLimitIsCheckedBeforeContentIsAccepted()
    {
        using var archive = TestArchive.Create(ValidEntries());
        var options = new ContentImportOptions { MaxEntryBytes = 16 };
        var reader = new ProblemPackageReader(options);

        var exception = await Assert.ThrowsAsync<PackageValidationException>(() =>
            reader.ReadAsync(archive.Path));

        Assert.Contains(
            exception.Errors,
            error => error.Contains("entry limit", StringComparison.Ordinal));
    }

    [Fact]
    public async Task UnknownManifestPropertiesAreRejected()
    {
        var entries = ValidEntries()
            .Select(entry => entry.Key == "problem.json"
                ? new KeyValuePair<string, string>(
                    entry.Key,
                    entry.Value.Replace(
                        "\"schemaVersion\": 1,",
                        "\"schemaVersion\": 1, \"unexpected\": true,",
                        StringComparison.Ordinal))
                : entry)
            .ToArray();
        using var archive = TestArchive.Create(entries);
        var reader = new ProblemPackageReader(new ContentImportOptions());

        var exception = await Assert.ThrowsAsync<PackageValidationException>(() =>
            reader.ReadAsync(archive.Path));

        Assert.Contains(
            exception.Errors,
            error => error.StartsWith("problem.json is invalid", StringComparison.Ordinal));
    }

    private static IReadOnlyCollection<KeyValuePair<string, string>> ValidEntries()
    {
        const string manifest = """
            {
              "schemaVersion": 1,
              "slug": "two-sum",
              "title": "Two Sum",
              "difficulty": "Easy",
              "timeLimitMs": 1000,
              "memoryLimitKb": 262144,
              "tags": [
                { "slug": "array", "name": "Array" },
                { "slug": "hash-table", "name": "Hash Table" }
              ]
            }
            """;

        return new Dictionary<string, string>
        {
            ["problem.json"] = manifest,
            ["statement.md"] = "# Two Sum\nFind two indices.",
            ["constraints.md"] = "- At least two values.",
            ["samples/01.in"] = "4\n2 7 11 15\n9\n",
            ["samples/01.out"] = "0 1\n",
            ["samples/01.md"] = "Example explanation",
            ["tests/001.in"] = "4\n2 7 11 15\n9\n",
            ["tests/001.out"] = "0 1\n",
            ["tests/002.in"] = "3\n3 2 4\n6\n",
            ["tests/002.out"] = "1 2\n"
        };
    }

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
                $"algojudge-content-{Guid.NewGuid():N}.zip");

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
