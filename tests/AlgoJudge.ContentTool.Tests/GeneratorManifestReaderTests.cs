using AlgoJudge.ContentTool.Generation;

namespace AlgoJudge.ContentTool.Tests;

public sealed class GeneratorManifestReaderTests
{
    [Fact]
    public async Task StrictManifestIsReadWithSeedGroups()
    {
        using var directory = ManifestDirectory.Create(ValidManifest());
        var reader = new GeneratorManifestReader(maximumTestCaseCount: 500);

        var manifest = await reader.ReadAsync(directory.Path);

        Assert.Equal(2, manifest.Groups.Count);
        Assert.Equal(12, manifest.Groups.Sum(group => group.Count));
    }

    [Fact]
    public async Task RequestedCountAbovePackageLimitIsRejected()
    {
        using var directory = ManifestDirectory.Create(
            ValidManifest().Replace("\"count\": 10", "\"count\": 501", StringComparison.Ordinal));
        var reader = new GeneratorManifestReader(maximumTestCaseCount: 500);

        var exception = await Assert.ThrowsAsync<TestGenerationException>(() =>
            reader.ReadAsync(directory.Path));

        Assert.Contains("limit is 500", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnsafeComponentPathIsRejected()
    {
        using var directory = ManifestDirectory.Create(
            ValidManifest().Replace(
                "generator/component.dll",
                "../component.dll",
                StringComparison.Ordinal));
        var reader = new GeneratorManifestReader(maximumTestCaseCount: 500);

        var exception = await Assert.ThrowsAsync<TestGenerationException>(() =>
            reader.ReadAsync(directory.Path));

        Assert.Contains("unsafe path", exception.Message, StringComparison.Ordinal);
    }

    private static string ValidManifest() =>
        """
        {
          "schemaVersion": 1,
          "generator": {
            "type": "dotnet",
            "assembly": "generator/component.dll",
            "entry": "Tests.Generator"
          },
          "inputValidator": {
            "type": "dotnet",
            "assembly": "generator/component.dll",
            "entry": "Tests.Validator"
          },
          "groups": [
            { "name": "edge", "seed": 101, "count": 2 },
            { "name": "random", "seed": 202, "count": 10 }
          ],
          "referenceSolution": {
            "type": "cpp17",
            "path": "reference/solution.cpp"
          }
        }
        """;

    private sealed class ManifestDirectory : IDisposable
    {
        private ManifestDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static ManifestDirectory Create(string manifest)
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"algojudge-manifest-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(System.IO.Path.Combine(path, "generator"));
            Directory.CreateDirectory(System.IO.Path.Combine(path, "reference"));
            File.WriteAllText(
                System.IO.Path.Combine(path, "generator", "manifest.json"),
                manifest);
            File.WriteAllText(
                System.IO.Path.Combine(path, "generator", "component.dll"),
                "component");
            File.WriteAllText(
                System.IO.Path.Combine(path, "reference", "solution.cpp"),
                "int main() { return 0; }");
            return new ManifestDirectory(path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
