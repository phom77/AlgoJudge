using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AlgoJudge.ContentTool.Generation;

internal sealed class GeneratedTestSuiteStore
{
    public const string RelativeManifestPath = "generator/generated-tests.json";

    private static readonly UTF8Encoding Utf8NoBom = new(false);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true
    };

    public async Task WriteAsync(
        string problemDirectory,
        IReadOnlyList<GeneratedTestCase> testCases,
        GeneratedSuiteManifest manifest,
        CancellationToken cancellationToken)
    {
        var testsPath = Path.Combine(problemDirectory, "tests");
        var manifestPath = Path.Combine(problemDirectory, "generator", "generated-tests.json");
        await EnsureExistingTestsAreOwnedAsync(
            problemDirectory,
            testsPath,
            manifestPath,
            cancellationToken);

        var temporaryPath = Path.Combine(
            problemDirectory,
            $".tests-generating-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryPath);
        try
        {
            foreach (var testCase in testCases)
            {
                var stem = testCase.Ordinal.ToString("D3", System.Globalization.CultureInfo.InvariantCulture);
                await File.WriteAllTextAsync(
                    Path.Combine(temporaryPath, $"{stem}.in"),
                    testCase.Input,
                    Utf8NoBom,
                    cancellationToken);
                await File.WriteAllTextAsync(
                    Path.Combine(temporaryPath, $"{stem}.out"),
                    testCase.Output,
                    Utf8NoBom,
                    cancellationToken);
            }

            var backupPath = Path.Combine(
                problemDirectory,
                $".tests-backup-{Guid.NewGuid():N}");
            if (Directory.Exists(testsPath))
                Directory.Move(testsPath, backupPath);

            try
            {
                Directory.Move(temporaryPath, testsPath);
                var manifestJson = JsonSerializer.Serialize(manifest, JsonOptions) + "\n";
                await File.WriteAllTextAsync(
                    manifestPath,
                    manifestJson,
                    Utf8NoBom,
                    cancellationToken);
                if (Directory.Exists(backupPath))
                    Directory.Delete(backupPath, recursive: true);
            }
            catch
            {
                if (Directory.Exists(testsPath))
                    Directory.Delete(testsPath, recursive: true);
                if (Directory.Exists(backupPath))
                    Directory.Move(backupPath, testsPath);
                throw;
            }
        }
        finally
        {
            if (Directory.Exists(temporaryPath))
                Directory.Delete(temporaryPath, recursive: true);
        }
    }

    public async Task<GeneratedSuiteManifest> ReadManifestAsync(
        string problemDirectory,
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(problemDirectory, "generator", "generated-tests.json");
        if (!File.Exists(path))
        {
            throw new TestGenerationException(
                $"Generated suite manifest is missing: {RelativeManifestPath}.");
        }

        try
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken);
            var manifest = JsonSerializer.Deserialize<GeneratedSuiteManifest>(json, JsonOptions);
            if (manifest is null || manifest.SchemaVersion is not (1 or 2))
                throw new TestGenerationException("Generated suite manifest schemaVersion must be 1 or 2.");
            return manifest;
        }
        catch (JsonException)
        {
            throw new TestGenerationException("Generated suite manifest is invalid.");
        }
    }

    private async Task EnsureExistingTestsAreOwnedAsync(
        string problemDirectory,
        string testsPath,
        string manifestPath,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(testsPath) || !Directory.EnumerateFileSystemEntries(testsPath).Any())
            return;

        if (!File.Exists(manifestPath))
        {
            throw new TestGenerationException(
                "The tests directory is not owned by the generator; refusing to overwrite it.");
        }

        var manifest = await ReadManifestAsync(problemDirectory, cancellationToken);
        var expectedFiles = manifest.Cases
            .SelectMany(testCase => new[]
            {
                testCase.Ordinal.ToString("D3", System.Globalization.CultureInfo.InvariantCulture) + ".in",
                testCase.Ordinal.ToString("D3", System.Globalization.CultureInfo.InvariantCulture) + ".out"
            })
            .ToHashSet(StringComparer.Ordinal);
        var actualFiles = Directory.EnumerateFiles(testsPath)
            .Select(Path.GetFileName)
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);
        if (Directory.EnumerateDirectories(testsPath).Any() ||
            !actualFiles.SetEquals(expectedFiles))
        {
            throw new TestGenerationException(
                "The tests directory contains files not owned by the generator.");
        }

        foreach (var testCase in manifest.Cases)
        {
            var stem = testCase.Ordinal.ToString(
                "D3",
                System.Globalization.CultureInfo.InvariantCulture);
            var input = await File.ReadAllTextAsync(
                Path.Combine(testsPath, $"{stem}.in"),
                cancellationToken);
            var output = await File.ReadAllTextAsync(
                Path.Combine(testsPath, $"{stem}.out"),
                cancellationToken);
            if (!string.Equals(ContentHash.Sha256(input), testCase.InputSha256, StringComparison.Ordinal) ||
                !string.Equals(ContentHash.Sha256(output), testCase.OutputSha256, StringComparison.Ordinal))
            {
                throw new TestGenerationException(
                    "The tests directory has changed since it was generated; refusing to overwrite it.");
            }
        }
    }
}
