using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace AlgoJudge.Infrastructure.Health;

public sealed class PostgreSqlHealthCheck(string connectionString) : IHealthCheck
{
    public static string ValidateConnectionString(string? connectionString, string processName)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"ConnectionStrings:DefaultConnection must be configured for the {processName}.");
        }

        NpgsqlConnectionStringBuilder builder;
        try
        {
            builder = new NpgsqlConnectionStringBuilder(connectionString);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection is not a valid PostgreSQL connection string.",
                exception);
        }

        if (string.IsNullOrWhiteSpace(builder.Host) ||
            string.IsNullOrWhiteSpace(builder.Database) ||
            string.IsNullOrWhiteSpace(builder.Username))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection must include Host, Database, and Username.");
        }

        return connectionString;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            await command.ExecuteScalarAsync(cancellationToken);
            return HealthCheckResult.Healthy("PostgreSQL is reachable.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy(
                "PostgreSQL is not reachable.",
                exception);
        }
    }
}
