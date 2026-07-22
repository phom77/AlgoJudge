using AlgoJudge.Application.ContentGeneration;

namespace AlgoJudge.ContentTool.Generation;

public sealed record ProblemAuthoringDocument(
    ProblemAuthoringDefinition Definition,
    string DefinitionSha256);
