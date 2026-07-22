namespace AlgoJudge.ContentTool.Tests;

[AttributeUsage(AttributeTargets.Method)]
public sealed class DotNetGenerationSandboxFactAttribute : FactAttribute
{
    public const string ImageEnvironmentVariable = "TEST_DOTNET_GENERATOR_IMAGE";

    public DotNetGenerationSandboxFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ImageEnvironmentVariable)))
            Skip = $"Set {ImageEnvironmentVariable} to run .NET generation sandbox tests.";
    }
}
