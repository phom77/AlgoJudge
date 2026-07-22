namespace AlgoJudge.Application.Models.ContentGeneration;

public sealed record ContentGenerationResult(
    string SuiteSha256,
    string Toolchain,
    IReadOnlyList<GeneratedContentCase> Cases,
    IReadOnlyDictionary<string, int> CasesByGroup,
    int WrongSolutionCount,
    IReadOnlyDictionary<string, int> KilledCaseCountByWrongSolution,
    IReadOnlyList<string> SurvivingWrongSolutions);
