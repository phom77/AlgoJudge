using AlgoJudge.Application.FunctionExecution;

namespace AlgoJudge.Application.Contracts.Problems;

public sealed class FunctionParameterResponse
{
    public string Name { get; init; } = string.Empty;
    public FunctionValueType Type { get; init; }
}
