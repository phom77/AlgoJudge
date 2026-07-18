using AlgoJudge.Application.ContentGeneration;
using AlgoJudge.ContentTool.Configuration;
using AlgoJudge.ContentTool.Generation;

namespace AlgoJudge.ContentTool.Tests;

public sealed class TestGenerationServiceTests
{
    [Fact]
    public async Task GenerateAndValidateGeneratedProduceDeterministicHashedSuite()
    {
        using var directory = GenerationDirectory.Create();
        var manifest = directory.CreateManifest(count: 3);
        var service = new TestGenerationService(new ContentImportOptions());
        var generator = new DeterministicGenerator();
        var validator = new AcceptingValidator();
        var reference = new EchoReferenceRunner();

        var generated = await service.GenerateAsync(
            directory.Path,
            manifest,
            generator,
            validator,
            reference);
        var validated = await service.ValidateGeneratedAsync(
            directory.Path,
            manifest,
            generator,
            validator,
            reference);

        Assert.Equal(3, generated.TestCaseCount);
        Assert.Equal(generated, validated);
        Assert.True(File.Exists(System.IO.Path.Combine(directory.Path, "tests", "001.in")));
        Assert.True(File.Exists(System.IO.Path.Combine(
            directory.Path,
            "generator",
            "generated-tests.json")));
        Assert.Equal(2, reference.InvocationCount);
    }

    [Fact]
    public async Task NonDeterministicGeneratorIsRejectedBeforeReferenceExecution()
    {
        using var directory = GenerationDirectory.Create();
        var manifest = directory.CreateManifest(count: 1);
        var service = new TestGenerationService(new ContentImportOptions());
        var reference = new EchoReferenceRunner();

        var exception = await Assert.ThrowsAsync<TestGenerationException>(() =>
            service.GenerateAsync(
                directory.Path,
                manifest,
                new NonDeterministicGenerator(),
                new AcceptingValidator(),
                reference));

        Assert.Contains("not deterministic", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, reference.InvocationCount);
        Assert.False(Directory.Exists(System.IO.Path.Combine(directory.Path, "tests")));
    }

    [Fact]
    public async Task InvalidGeneratedInputIsRejectedBeforeReferenceExecution()
    {
        using var directory = GenerationDirectory.Create();
        var manifest = directory.CreateManifest(count: 1);
        var service = new TestGenerationService(new ContentImportOptions());
        var reference = new EchoReferenceRunner();

        var exception = await Assert.ThrowsAsync<TestGenerationException>(() =>
            service.GenerateAsync(
                directory.Path,
                manifest,
                new DeterministicGenerator(),
                new RejectingValidator(),
                reference));

        Assert.Contains("failed validation", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, reference.InvocationCount);
    }

    [Fact]
    public async Task ExistingManualTestsAreNotOverwritten()
    {
        using var directory = GenerationDirectory.Create();
        var testsPath = System.IO.Path.Combine(directory.Path, "tests");
        Directory.CreateDirectory(testsPath);
        var manualPath = System.IO.Path.Combine(testsPath, "001.in");
        await File.WriteAllTextAsync(manualPath, "manual");
        var service = new TestGenerationService(new ContentImportOptions());

        var exception = await Assert.ThrowsAsync<TestGenerationException>(() =>
            service.GenerateAsync(
                directory.Path,
                directory.CreateManifest(count: 1),
                new DeterministicGenerator(),
                new AcceptingValidator(),
                new EchoReferenceRunner()));

        Assert.Contains("refusing to overwrite", exception.Message, StringComparison.Ordinal);
        Assert.Equal("manual", await File.ReadAllTextAsync(manualPath));
    }

    [Fact]
    public async Task ChangedGeneratedFileIsRejected()
    {
        using var directory = GenerationDirectory.Create();
        var manifest = directory.CreateManifest(count: 1);
        var service = new TestGenerationService(new ContentImportOptions());
        var generator = new DeterministicGenerator();
        var validator = new AcceptingValidator();
        var reference = new EchoReferenceRunner();
        await service.GenerateAsync(directory.Path, manifest, generator, validator, reference);
        await File.AppendAllTextAsync(
            System.IO.Path.Combine(directory.Path, "tests", "001.out"),
            "tampered");

        var exception = await Assert.ThrowsAsync<TestGenerationException>(() =>
            service.ValidateGeneratedAsync(
                directory.Path,
                manifest,
                generator,
                validator,
                reference));

        Assert.Contains("does not match", exception.Message, StringComparison.Ordinal);
    }

    private sealed class DeterministicGenerator : ITestCaseGenerator
    {
        public Task<string> GenerateAsync(
            TestCaseGenerationContext context,
            CancellationToken cancellationToken = default) =>
            Task.FromResult($"{context.Group} {context.Seed}\n");
    }

    private sealed class NonDeterministicGenerator : ITestCaseGenerator
    {
        private int _value;

        public Task<string> GenerateAsync(
            TestCaseGenerationContext context,
            CancellationToken cancellationToken = default) =>
            Task.FromResult($"{++_value}\n");
    }

    private sealed class AcceptingValidator : IInputValidator
    {
        public Task<InputValidationResult> ValidateAsync(
            string input,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(InputValidationResult.Valid);
    }

    private sealed class RejectingValidator : IInputValidator
    {
        public Task<InputValidationResult> ValidateAsync(
            string input,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(InputValidationResult.Invalid("bad input"));
    }

    private sealed class EchoReferenceRunner : IReferenceSolutionRunner
    {
        public int InvocationCount { get; private set; }

        public Task<IReadOnlyList<string>> RunAsync(
            string sourceCode,
            IReadOnlyList<string> inputs,
            ReferenceSolutionLimits limits,
            CancellationToken cancellationToken = default)
        {
            InvocationCount++;
            return Task.FromResult<IReadOnlyList<string>>(
                inputs.Select(input => $"expected:{input}").ToArray());
        }
    }

    private sealed class GenerationDirectory : IDisposable
    {
        private GenerationDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static GenerationDirectory Create()
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"algojudge-generation-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(System.IO.Path.Combine(path, "generator"));
            Directory.CreateDirectory(System.IO.Path.Combine(path, "reference"));
            File.WriteAllText(
                System.IO.Path.Combine(path, "problem.json"),
                "{\"timeLimitMs\":1000,\"memoryLimitKb\":262144}");
            File.WriteAllText(
                System.IO.Path.Combine(path, "generator", "manifest.json"),
                "{\"schemaVersion\":1}");
            File.WriteAllText(
                System.IO.Path.Combine(path, "generator", "component.dll"),
                "test component bytes");
            File.WriteAllText(
                System.IO.Path.Combine(path, "reference", "solution.cpp"),
                "int main() { return 0; }");
            return new GenerationDirectory(path);
        }

        public GeneratorManifest CreateManifest(int count) => new()
        {
            SchemaVersion = 1,
            Generator = new DotNetComponentManifest
            {
                Type = "dotnet",
                Assembly = "generator/component.dll",
                Entry = "Tests.Generator"
            },
            InputValidator = new DotNetComponentManifest
            {
                Type = "dotnet",
                Assembly = "generator/component.dll",
                Entry = "Tests.Validator"
            },
            Groups =
            [
                new GeneratorGroupManifest { Name = "edge", Seed = 101, Count = count }
            ],
            ReferenceSolution = new ReferenceSolutionManifest
            {
                Type = "cpp17",
                Path = "reference/solution.cpp"
            }
        };

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
