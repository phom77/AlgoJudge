namespace AlgoJudge.Application.ContentGeneration;

public sealed record TestCaseGenerationContext(
    int Ordinal,
    string Group,
    int Seed);
