namespace AlgoJudge.Application.ContentGeneration;

public interface ITestCaseGenerator
{
    Task<string> GenerateAsync(
        TestCaseGenerationContext context,
        CancellationToken cancellationToken = default);
}
