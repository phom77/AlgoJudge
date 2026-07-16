namespace AlgoJudge.Backend.EndToEndTests;

[AttributeUsage(AttributeTargets.Method)]
public sealed class BackendEndToEndFactAttribute : FactAttribute
{
    public const string PostgreSqlConnectionEnvironmentVariable =
        "TEST_POSTGRES_CONNECTION";
    public const string DockerImageEnvironmentVariable =
        "TEST_DOCKER_JUDGE_IMAGE";

    public BackendEndToEndFactAttribute()
    {
        var missingPrerequisites = new List<string>();
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(
                PostgreSqlConnectionEnvironmentVariable)))
        {
            missingPrerequisites.Add(PostgreSqlConnectionEnvironmentVariable);
        }

        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(
                DockerImageEnvironmentVariable)))
        {
            missingPrerequisites.Add(DockerImageEnvironmentVariable);
        }

        if (missingPrerequisites.Count > 0)
        {
            Skip = $"Set {string.Join(" and ", missingPrerequisites)} to run backend E2E acceptance tests.";
        }
    }
}
