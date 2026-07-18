using Microsoft.AspNetCore.DataProtection;

namespace AlgoJudge.API.Security;

internal static class DataProtectionConfiguration
{
    private const string ApplicationName = "AlgoJudge";

    public static void AddConfiguredDataProtection(WebApplicationBuilder builder)
    {
        var dataProtection = builder.Services
            .AddDataProtection()
            .SetApplicationName(ApplicationName);
        var configuredKeysPath = builder.Configuration["DataProtection:KeysPath"];
        if (string.IsNullOrWhiteSpace(configuredKeysPath))
            return;

        string keysPath;
        try
        {
            keysPath = Path.IsPathRooted(configuredKeysPath)
                ? Path.GetFullPath(configuredKeysPath)
                : Path.GetFullPath(configuredKeysPath, builder.Environment.ContentRootPath);
            Directory.CreateDirectory(keysPath);
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or NotSupportedException or
            UnauthorizedAccessException)
        {
            throw new InvalidOperationException(
                "DataProtection:KeysPath is invalid or cannot be accessed.",
                exception);
        }

        dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
    }
}
