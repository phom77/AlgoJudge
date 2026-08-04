namespace AlgoJudge.ContentTool.Tests;

[AttributeUsage(AttributeTargets.Method)]
public sealed class PracticeCatalogPipelineFactAttribute : FactAttribute
{
    public const string GeneratorImageVariable = "TEST_DOTNET_GENERATOR_IMAGE";
    public const string JudgeImageVariable = "TEST_DOCKER_JUDGE_IMAGE";

    public PracticeCatalogPipelineFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable(GeneratorImageVariable)) ||
            string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable(JudgeImageVariable)))
        {
            Skip = $"Set {GeneratorImageVariable} and {JudgeImageVariable} to run the practice catalog pipeline.";
        }
    }
}
