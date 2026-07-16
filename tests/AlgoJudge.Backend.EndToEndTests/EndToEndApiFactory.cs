extern alias Api;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using ApiProgram = Api::Program;

namespace AlgoJudge.Backend.EndToEndTests;

internal sealed class EndToEndApiFactory : WebApplicationFactory<ApiProgram>
{
    private readonly CapturingLoggerProvider _loggerProvider;

    public EndToEndApiFactory(
        string connectionString,
        CapturingLoggerProvider loggerProvider)
    {
        _loggerProvider = loggerProvider;
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__DefaultConnection",
            connectionString);
        Environment.SetEnvironmentVariable("Jwt__Issuer", "AlgoJudge.EndToEndTests");
        Environment.SetEnvironmentVariable(
            "Jwt__Audience",
            "AlgoJudge.EndToEndTests.Client");
        Environment.SetEnvironmentVariable(
            "Jwt__SecretKey",
            "backend-e2e-secret-key-at-least-32-characters");
        Environment.SetEnvironmentVariable("Jwt__ExpiresInHours", "1");
        Environment.SetEnvironmentVariable("Jwt__RefreshTokenExpiryDays", "1");
        Environment.SetEnvironmentVariable("RateLimiting__PermitLimit", "200");
        Environment.SetEnvironmentVariable("RateLimiting__WindowSeconds", "60");
        Environment.SetEnvironmentVariable("RateLimiting__QueueLimit", "0");
        Environment.SetEnvironmentVariable("Database__MigrateOnStartup", "false");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging => logging.AddProvider(_loggerProvider));
    }
}
