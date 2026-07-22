namespace AlgoJudge.ProblemGeneratorSdk;

public sealed record GeneratedPlanCase(
    int Ordinal,
    string Name,
    string Group,
    int Seed,
    TestArguments Arguments);
