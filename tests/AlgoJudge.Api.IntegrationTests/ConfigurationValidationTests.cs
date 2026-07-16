using AlgoJudge.API.Configuration;
using AlgoJudge.Infrastructure.Health;

namespace AlgoJudge.Api.IntegrationTests;

public class ConfigurationValidationTests
{
    [Fact]
    public void JwtSecretMustBeAtLeastThirtyTwoCharacters()
    {
        var options = new JwtOptions
        {
            Issuer = "issuer",
            Audience = "audience",
            SecretKey = "too-short",
            ExpiresInHours = 1,
            RefreshTokenExpiryDays = 1
        };

        var exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Contains("at least 32", exception.Message);
    }

    [Fact]
    public void RateLimitValuesAreBounded()
    {
        var options = new RateLimitingOptions
        {
            PermitLimit = 0,
            WindowSeconds = 60
        };

        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    [Fact]
    public void PostgreSqlConnectionRequiresCoreFields()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            PostgreSqlHealthCheck.ValidateConnectionString(
                "Host=localhost;Database=algojudge",
                "API"));

        Assert.Contains("Username", exception.Message);
    }
}
