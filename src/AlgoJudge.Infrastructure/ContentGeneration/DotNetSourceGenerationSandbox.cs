using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AlgoJudge.Application.ContentGeneration;
using AlgoJudge.Infrastructure.Grading;
using AlgoJudge.ProblemGeneratorSdk;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AlgoJudge.Infrastructure.ContentGeneration;

public sealed class DotNetSourceGenerationSandbox : ISourceGenerationSandbox
{
    private const string ContainerUser = "10001:10001";
    private const string ContainerWorkspace = "/workspace";
    private const string ContainerArtifact = "/artifact";
    private const string SdkPath = "/sdk/AlgoJudge.ProblemGeneratorSdk.dll";

    private static readonly UTF8Encoding Utf8NoBom = new(false);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    private readonly DotNetSourceSandboxOptions _options;
    private readonly DockerCli _docker;

    public DotNetSourceGenerationSandbox(
        IConfiguration configuration,
        ILogger<DotNetSourceGenerationSandbox> logger)
    {
        _options = DotNetSourceSandboxOptions.FromConfiguration(configuration);
        _docker = new DockerCli(_options.DockerStartupAllowance, logger);
    }

    public async Task<SourceGenerationResult> GenerateAsync(
        SourceGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);
        var workDirectory = Path.Combine(
            Path.GetTempPath(),
            "algojudge-content-source",
            Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(workDirectory);
            PrepareDirectory(workDirectory);
            await WriteSourcesAsync(workDirectory, request, cancellationToken);
            await CompileAsync(workDirectory, cancellationToken);
            return await RunAsync(workDirectory, request, cancellationToken);
        }
        finally
        {
            if (Directory.Exists(workDirectory))
                Directory.Delete(workDirectory, recursive: true);
        }
    }

    private async Task CompileAsync(string workDirectory, CancellationToken cancellationToken)
    {
        var containerName = CreateContainerName("compile");
        try
        {
            var arguments = BuildBaseCreateArguments(containerName);
            arguments.AddRange([
                "--tmpfs", "/tmp:rw,nosuid,nodev,size=128m",
                "--volume", $"{ToDockerPath(workDirectory)}:{ContainerWorkspace}:rw",
                "--volume", $"{ToDockerPath(typeof(ProblemGenerator).Assembly.Location)}:{SdkPath}:ro",
                "--workdir", ContainerWorkspace,
                "--env", "DOTNET_CLI_HOME=/tmp",
                "--env", "HOME=/tmp",
                _options.Image,
                "dotnet", "publish", $"{ContainerWorkspace}/GeneratorHost.csproj",
                "--configuration", "Release",
                "--output", $"{ContainerWorkspace}/publish",
                "--nologo", "--ignore-failed-sources"
            ]);
            await _docker.CreateAsync(arguments, cancellationToken);
            var result = await _docker.StartAsync(
                containerName,
                stdin: null,
                _options.CompileTimeout,
                stdoutLimitBytes: 1024 * 1024,
                stderrLimitBytes: 1024 * 1024,
                cancellationToken);
            var state = await _docker.InspectAsync(containerName, cancellationToken);
            if (!state.Status.Equals("exited", StringComparison.OrdinalIgnoreCase) || state.ExitCode != 0)
            {
                var diagnostics = string.Join(
                    Environment.NewLine,
                    new[] { Decode(result.Stdout.Bytes), Decode(result.Stderr.Bytes) }
                        .Where(value => !string.IsNullOrWhiteSpace(value)));
                throw new InvalidOperationException(
                    $"Generator source did not compile: {diagnostics}");
            }

            if (!File.Exists(Path.Combine(workDirectory, "publish", "GeneratorHost.dll")))
                throw new InvalidOperationException("Generator compiler produced no executable artifact.");
        }
        finally
        {
            await _docker.RemoveAsync(containerName);
        }
    }

    private async Task<SourceGenerationResult> RunAsync(
        string workDirectory,
        SourceGenerationRequest request,
        CancellationToken cancellationToken)
    {
        var publishDirectory = Path.Combine(workDirectory, "publish");
        var containerName = CreateContainerName("run");
        try
        {
            var arguments = BuildBaseCreateArguments(containerName);
            arguments.AddRange([
                "--interactive",
                "--tmpfs", "/tmp:rw,nosuid,nodev,size=64m",
                "--volume", $"{ToDockerPath(publishDirectory)}:{ContainerArtifact}:ro",
                "--workdir", ContainerArtifact,
                "--env", "DOTNET_CLI_HOME=/tmp",
                "--env", "HOME=/tmp",
                _options.Image,
                "dotnet", $"{ContainerArtifact}/GeneratorHost.dll"
            ]);
            await _docker.CreateAsync(arguments, cancellationToken);
            var input = JsonSerializer.Serialize(request, JsonOptions);
            var result = await _docker.StartAsync(
                containerName,
                input,
                _options.RunTimeout,
                _options.OutputLimitBytes,
                1024 * 1024,
                cancellationToken);
            var state = await _docker.InspectAsync(containerName, cancellationToken);
            if (state.OomKilled)
                throw new InvalidOperationException("Generator exceeded its memory limit.");
            if (!state.Status.Equals("exited", StringComparison.OrdinalIgnoreCase) || state.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Generator execution failed: {Decode(result.Stderr.Bytes)}");
            }
            if (result.Stdout.Truncated)
                throw new InvalidOperationException("Generator output exceeded its configured limit.");

            try
            {
                var response = JsonSerializer.Deserialize<HostResponse>(result.Stdout.Bytes, JsonOptions)
                    ?? throw new InvalidOperationException("Generator returned an empty response.");
                if (response.Cases is null || response.Cases.Count > request.MaximumCaseCount)
                    throw new InvalidOperationException("Generator returned an invalid case collection.");
                return new SourceGenerationResult(response.Cases, _options.Image);
            }
            catch (JsonException exception)
            {
                throw new InvalidOperationException("Generator returned an invalid response.", exception);
            }
        }
        finally
        {
            await _docker.RemoveAsync(containerName);
        }
    }

    private List<string> BuildBaseCreateArguments(string containerName) =>
    [
        "create",
        "--name", containerName,
        "--network", "none",
        "--memory", $"{_options.MemoryMb}m",
        "--memory-swap", $"{_options.MemoryMb}m",
        "--cpus", "1",
        "--pids-limit", _options.PidsLimit.ToString(CultureInfo.InvariantCulture),
        "--cap-drop", "ALL",
        "--security-opt", "no-new-privileges=true",
        "--read-only",
        "--user", ContainerUser,
        "--ulimit", "core=0:0",
        "--ulimit", "nofile=64:64"
    ];

    private static async Task WriteSourcesAsync(
        string workDirectory,
        SourceGenerationRequest request,
        CancellationToken cancellationToken)
    {
        await File.WriteAllTextAsync(
            Path.Combine(workDirectory, "GeneratorHost.csproj"),
            DotNetSourceGenerationHost.Project,
            Utf8NoBom,
            cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(workDirectory, "Generator.cs"),
            request.GeneratorSource,
            Utf8NoBom,
            cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(workDirectory, "Validator.cs"),
            request.ValidatorSource,
            Utf8NoBom,
            cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(workDirectory, "HostProgram.cs"),
            DotNetSourceGenerationHost.Source,
            Utf8NoBom,
            cancellationToken);
    }

    private static void PrepareDirectory(string path)
    {
        if (!OperatingSystem.IsLinux())
            return;
        File.SetUnixFileMode(
            path,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute);
    }

    private static string CreateContainerName(string stage) =>
        $"algojudge-content-{stage}-{Guid.NewGuid():N}";

    private static string Decode(byte[] value) => Encoding.UTF8.GetString(value);

    private static void ValidateRequest(SourceGenerationRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.GeneratorSource);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ValidatorSource);
        ArgumentNullException.ThrowIfNull(request.ParameterNames);
        ArgumentNullException.ThrowIfNull(request.HandwrittenCases);
        if (request.MaximumCaseCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(request), "Maximum case count must be positive.");
        if (request.HandwrittenCases.Count > request.MaximumCaseCount)
            throw new ArgumentException("Handwritten cases exceed the maximum case count.", nameof(request));
        if (request.ParameterNames.Any(string.IsNullOrWhiteSpace) ||
            request.ParameterNames.Distinct(StringComparer.Ordinal).Count() != request.ParameterNames.Count)
        {
            throw new ArgumentException("Parameter names must be non-empty and unique.", nameof(request));
        }
    }

    private static string ToDockerPath(string path)
    {
        if (!OperatingSystem.IsWindows())
            return path;
        return "/" + char.ToLowerInvariant(path[0]) + path[2..].Replace('\\', '/');
    }

    private sealed record HostResponse(IReadOnlyList<SourceGeneratedCase> Cases);
}
