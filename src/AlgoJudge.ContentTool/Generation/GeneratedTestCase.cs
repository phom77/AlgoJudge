namespace AlgoJudge.ContentTool.Generation;

internal sealed record GeneratedTestCase(
    int Ordinal,
    string Group,
    int Seed,
    string Input,
    string Output);
