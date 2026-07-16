namespace AlgoJudge.Judge.IntegrationTests;

[AttributeUsage(AttributeTargets.Method)]
public sealed class DockerJudgeFactAttribute : FactAttribute
{
    public const string ImageEnvironmentVariable = "TEST_DOCKER_JUDGE_IMAGE";

    public DockerJudgeFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable(ImageEnvironmentVariable)))
        {
            Skip = $"Set {ImageEnvironmentVariable} to run Docker judge integration tests.";
        }
    }
}
