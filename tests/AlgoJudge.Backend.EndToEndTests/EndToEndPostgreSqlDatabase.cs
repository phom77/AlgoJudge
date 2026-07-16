using AlgoJudge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AlgoJudge.Backend.EndToEndTests;

internal sealed class EndToEndPostgreSqlDatabase : IAsyncDisposable
{
    private readonly string _adminConnectionString;
    private readonly string _databaseName;

    private EndToEndPostgreSqlDatabase(
        string adminConnectionString,
        string databaseName,
        string connectionString)
    {
        _adminConnectionString = adminConnectionString;
        _databaseName = databaseName;
        ConnectionString = connectionString;
    }

    public string ConnectionString { get; }

    public static async Task<EndToEndPostgreSqlDatabase> CreateAsync()
    {
        var configuredConnection = Environment.GetEnvironmentVariable(
            BackendEndToEndFactAttribute.PostgreSqlConnectionEnvironmentVariable)
            ?? throw new InvalidOperationException(
                "PostgreSQL acceptance-test connection is not configured.");
        var adminBuilder = new NpgsqlConnectionStringBuilder(configuredConnection);
        var databaseName = $"algojudge_e2e_{Guid.NewGuid():N}";
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

        var database = new EndToEndPostgreSqlDatabase(
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

    public AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        return new AppDbContext(options);
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
