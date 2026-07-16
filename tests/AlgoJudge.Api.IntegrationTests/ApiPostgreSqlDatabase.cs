using AlgoJudge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AlgoJudge.Api.IntegrationTests;

public sealed class ApiPostgreSqlDatabase : IAsyncDisposable
{
    private readonly string _adminConnectionString;
    private readonly string _databaseName;

    private ApiPostgreSqlDatabase(
        string adminConnectionString,
        string databaseName,
        string connectionString)
    {
        _adminConnectionString = adminConnectionString;
        _databaseName = databaseName;
        ConnectionString = connectionString;
    }

    public string ConnectionString { get; }

    public static async Task<ApiPostgreSqlDatabase> CreateAsync()
    {
        var configuredConnection = Environment.GetEnvironmentVariable(
            PostgreSqlFactAttribute.ConnectionStringEnvironmentVariable)
            ?? throw new InvalidOperationException("PostgreSQL test connection is not configured.");
        var adminBuilder = new NpgsqlConnectionStringBuilder(configuredConnection);
        var databaseName = $"algojudge_api_{Guid.NewGuid():N}";
        var databaseBuilder = new NpgsqlConnectionStringBuilder(configuredConnection)
        {
            Database = databaseName,
            Pooling = false
        };

        await using (var connection = new NpgsqlConnection(adminBuilder.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE \"{databaseName}\"";
            await command.ExecuteNonQueryAsync();
        }

        var database = new ApiPostgreSqlDatabase(
            adminBuilder.ConnectionString,
            databaseName,
            databaseBuilder.ConnectionString);

        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(database.ConnectionString)
                .Options;
            await using var context = new AppDbContext(options);
            await context.Database.MigrateAsync();
            return database;
        }
        catch
        {
            await database.DisposeAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        await using var connection = new NpgsqlConnection(_adminConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)";
        await command.ExecuteNonQueryAsync();
    }
}
