namespace AlgoJudge.API.Configuration;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public int ExpiresInHours { get; set; }
    public int RefreshTokenExpiryDays { get; set; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Issuer))
            throw new InvalidOperationException("Jwt:Issuer must be configured.");
        if (string.IsNullOrWhiteSpace(Audience))
            throw new InvalidOperationException("Jwt:Audience must be configured.");
        if (SecretKey.Length < 32)
            throw new InvalidOperationException("Jwt:SecretKey must contain at least 32 characters.");
        if (ExpiresInHours is < 1 or > 24)
            throw new InvalidOperationException("Jwt:ExpiresInHours must be between 1 and 24.");
        if (RefreshTokenExpiryDays is < 1 or > 90)
        {
            throw new InvalidOperationException(
                "Jwt:RefreshTokenExpiryDays must be between 1 and 90.");
        }
    }
}

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public int PermitLimit { get; set; } = 100;
    public int WindowSeconds { get; set; } = 60;
    public int QueueLimit { get; set; }

    public void Validate()
    {
        if (PermitLimit is < 1 or > 10_000)
            throw new InvalidOperationException("RateLimiting:PermitLimit must be between 1 and 10000.");
        if (WindowSeconds is < 1 or > 3600)
        {
            throw new InvalidOperationException(
                "RateLimiting:WindowSeconds must be between 1 and 3600.");
        }
        if (QueueLimit is < 0 or > 1000)
            throw new InvalidOperationException("RateLimiting:QueueLimit must be between 0 and 1000.");
    }
}

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public bool MigrateOnStartup { get; set; }
}
