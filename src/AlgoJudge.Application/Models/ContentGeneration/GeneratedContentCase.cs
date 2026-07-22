namespace AlgoJudge.Application.Models.ContentGeneration;

public sealed record GeneratedContentCase(
    int Ordinal,
    string Name,
    string Group,
    int Seed,
    string Input,
    string ExpectedOutput,
    IReadOnlyList<string> KilledWrongSolutions);
