namespace AlgoJudge.Infrastructure.IntegrationTests;

[AttributeUsage(AttributeTargets.Method)]
public sealed class PostgreSqlFactAttribute : FactAttribute
{
    public const string ConnectionStringEnvironmentVariable = "TEST_POSTGRES_CONNECTION";

    public PostgreSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable)))
        {
            Skip = $"Set {ConnectionStringEnvironmentVariable} to run PostgreSQL integration tests.";
        }
    }
}
