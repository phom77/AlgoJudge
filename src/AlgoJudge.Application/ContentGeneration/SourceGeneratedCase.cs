namespace AlgoJudge.Application.ContentGeneration;

public sealed record SourceGeneratedCase(
    int Ordinal,
    string Name,
    string Group,
    int Seed,
    string Input);
