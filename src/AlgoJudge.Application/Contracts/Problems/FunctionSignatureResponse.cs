using AlgoJudge.Application.FunctionExecution;

namespace AlgoJudge.Application.Contracts.Problems;

public sealed class FunctionSignatureResponse
{
    public string ClassName { get; init; } = string.Empty;
    public string MethodName { get; init; } = string.Empty;
    public FunctionValueType ReturnType { get; init; }
    public IReadOnlyCollection<FunctionParameterResponse> Parameters { get; init; } = [];
}
