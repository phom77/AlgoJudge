using AlgoJudge.Application.FunctionExecution;

namespace AlgoJudge.Application.Contracts.Admin;

public sealed class UpdateFunctionSignatureRequest
{
    public FunctionSignature Signature { get; init; } = new();
}
