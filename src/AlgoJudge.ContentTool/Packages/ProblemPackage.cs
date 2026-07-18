using AlgoJudge.Domain.Enums;

namespace AlgoJudge.ContentTool.Packages;

public sealed class ProblemPackageMetadata
{
    public int SchemaVersion { get; init; }
    public string Slug { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public DifficultyLevel Difficulty { get; init; }
    public int TimeLimitMs { get; init; }
    public int MemoryLimitKb { get; init; }
    public ProblemExecutionMode ExecutionMode { get; init; } =
        ProblemExecutionMode.StdinStdout;
    public IReadOnlyCollection<ProblemPackageTag> Tags { get; init; } =
        Array.Empty<ProblemPackageTag>();
}

public sealed class ProblemPackageTag
{
    public string Slug { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
}

public sealed record ProblemPackageSample(
    int Ordinal,
    string Input,
    string ExpectedOutput,
    string? Explanation);

public sealed record ProblemPackageJudgeTestCase(
    int Ordinal,
    string Input,
    string ExpectedOutput);

public sealed record ProblemPackage(
    ProblemPackageMetadata Metadata,
    string StatementMarkdown,
    string ConstraintsMarkdown,
    IReadOnlyCollection<ProblemPackageSample> Samples,
    IReadOnlyCollection<ProblemPackageJudgeTestCase> JudgeTestCases,
    ProblemPackageFunction? Function = null);
