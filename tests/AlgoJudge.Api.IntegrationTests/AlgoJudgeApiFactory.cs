using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AlgoJudge.Api.IntegrationTests;

public sealed class AlgoJudgeApiFactory : WebApplicationFactory<Program>
{
    public AlgoJudgeApiFactory(string connectionString, int permitLimit = 100)
    {
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__DefaultConnection",
            connectionString);
        Environment.SetEnvironmentVariable("Jwt__Issuer", "AlgoJudge.IntegrationTests");
        Environment.SetEnvironmentVariable(
            "Jwt__Audience",
            "AlgoJudge.IntegrationTests.Client");
        Environment.SetEnvironmentVariable(
            "Jwt__SecretKey",
            "integration-test-secret-key-at-least-32-characters");
        Environment.SetEnvironmentVariable("Jwt__ExpiresInHours", "1");
        Environment.SetEnvironmentVariable("Jwt__RefreshTokenExpiryDays", "1");
        Environment.SetEnvironmentVariable(
            "RateLimiting__PermitLimit",
            permitLimit.ToString());
        Environment.SetEnvironmentVariable("RateLimiting__WindowSeconds", "60");
        Environment.SetEnvironmentVariable("RateLimiting__QueueLimit", "0");
        Environment.SetEnvironmentVariable("Database__MigrateOnStartup", "false");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
    }
}
