namespace AlgoJudge.Application.ContentGeneration;

public sealed record SourceGenerationResult(
    IReadOnlyList<SourceGeneratedCase> Cases,
    string ToolchainIdentity);
