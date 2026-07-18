using AlgoJudge.Application.FunctionExecution;

namespace AlgoJudge.ContentTool.Packages;

public sealed record ProblemPackageFunction(
    FunctionSignature Signature,
    string SignatureJson,
    string AdapterTemplate);
