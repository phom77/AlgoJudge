namespace AlgoJudge.Application.ContentGeneration;

public sealed record SourceGenerationRequest(
    string GeneratorSource,
    string ValidatorSource,
    int RootSeed,
    int MaximumCaseCount,
    IReadOnlyList<string> ParameterNames,
    IReadOnlyList<SourceHandwrittenCase> HandwrittenCases);
