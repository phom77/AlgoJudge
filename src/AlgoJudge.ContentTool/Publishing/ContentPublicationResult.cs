using AlgoJudge.Domain.Enums;

namespace AlgoJudge.ContentTool.Publishing;

public sealed record ContentPublicationResult(
    int ProblemId,
    string Slug,
    ProblemStatus Status,
    bool Changed);
