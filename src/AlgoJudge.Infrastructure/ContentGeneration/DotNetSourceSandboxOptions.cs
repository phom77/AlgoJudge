using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace AlgoJudge.Infrastructure.ContentGeneration;

public sealed record DotNetSourceSandboxOptions(
    string Image,
    TimeSpan CompileTimeout,
    TimeSpan RunTimeout,
    TimeSpan DockerStartupAllowance,
    int MemoryMb,
    int PidsLimit,
    int OutputLimitBytes)
{
    public static DotNetSourceSandboxOptions FromConfiguration(IConfiguration configuration)
    {
        var image = configuration["DotNetGenerationSandbox:DockerImage"]?.Trim();
        if (string.IsNullOrWhiteSpace(image) ||
            image.EndsWith(":latest", StringComparison.OrdinalIgnoreCase) ||
            image.EndsWith(":dev", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "DotNetGenerationSandbox:DockerImage must reference a versioned image.");
        }

        return new DotNetSourceSandboxOptions(
            image,
            TimeSpan.FromSeconds(ReadInteger(configuration, "CompileTimeoutSeconds", 60, 1, 180)),
            TimeSpan.FromSeconds(ReadInteger(configuration, "RunTimeoutSeconds", 30, 1, 120)),
            TimeSpan.FromSeconds(ReadInteger(configuration, "DockerStartupAllowanceSeconds", 10, 1, 60)),
            ReadInteger(configuration, "MemoryMb", 512, 128, 2048),
            ReadInteger(configuration, "PidsLimit", 64, 8, 256),
            ReadInteger(configuration, "OutputLimitBytes", 16 * 1024 * 1024, 1024, 128 * 1024 * 1024));
    }

    private static int ReadInteger(
        IConfiguration configuration,
        string name,
        int defaultValue,
        int minimum,
        int maximum)
    {
        var key = $"DotNetGenerationSandbox:{name}";
        var text = configuration[key];
        if (string.IsNullOrWhiteSpace(text))
            return defaultValue;
        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ||
            value < minimum || value > maximum)
        {
            throw new InvalidOperationException($"{key} must be between {minimum} and {maximum}.");
        }
        return value;
    }
}
