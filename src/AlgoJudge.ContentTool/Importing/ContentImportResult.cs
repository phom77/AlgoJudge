namespace AlgoJudge.ContentTool.Importing;

public sealed record ContentImportResult(
    int ProblemId,
    string Slug,
    bool Replaced,
    int JudgeVersion,
    int SampleCount,
    int JudgeTestCaseCount);
