using Microsoft.Extensions.Configuration;
using System.Globalization;

namespace AlgoJudge.Infrastructure.Grading;

internal sealed record DockerSandboxOptions(
    string Image,
    int StdoutLimitBytes,
    int StderrLimitBytes,
    int PidsLimit,
    TimeSpan CompileTimeout,
    TimeSpan DockerStartupAllowance)
{
    public static DockerSandboxOptions FromConfiguration(IConfiguration configuration)
    {
        var image = configuration["Sandbox:DockerImage"]?.Trim()
            ?? throw new InvalidOperationException(
                "Sandbox:DockerImage must reference the versioned AlgoJudge judge image.");

        if (string.IsNullOrWhiteSpace(image) ||
            image.StartsWith("gcc:", StringComparison.OrdinalIgnoreCase) ||
            image.EndsWith(":latest", StringComparison.OrdinalIgnoreCase) ||
            image.EndsWith(":dev", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Sandbox:DockerImage must not use a floating compiler image or mutable development tag.");
        }

        return new DockerSandboxOptions(
            image,
            ReadBoundedInteger(
                configuration,
                "Sandbox:StdoutLimitBytes",
                defaultValue: 64 * 1024,
                minimum: 1,
                maximum: 1024 * 1024),
            ReadBoundedInteger(
                configuration,
                "Sandbox:StderrLimitBytes",
                defaultValue: 64 * 1024,
                minimum: 1,
                maximum: 1024 * 1024),
            ReadBoundedInteger(
                configuration,
                "Sandbox:PidsLimit",
                defaultValue: 32,
                minimum: 1,
                maximum: 256),
            TimeSpan.FromSeconds(ReadBoundedInteger(
                configuration,
                "Sandbox:CompileTimeoutSeconds",
                defaultValue: 30,
                minimum: 1,
                maximum: 120)),
            TimeSpan.FromSeconds(ReadBoundedInteger(
                configuration,
                "Sandbox:DockerStartupAllowanceSeconds",
                defaultValue: 10,
                minimum: 1,
                maximum: 60)));
    }

    private static int ReadBoundedInteger(
        IConfiguration configuration,
        string key,
        int defaultValue,
        int minimum,
        int maximum)
    {
        var configured = configuration[key];
        if (string.IsNullOrWhiteSpace(configured))
            return defaultValue;

        if (!int.TryParse(
                configured,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var value) ||
            value < minimum ||
            value > maximum)
        {
            throw new InvalidOperationException(
                $"{key} must be between {minimum} and {maximum}.");
        }

        return value;
    }
}
