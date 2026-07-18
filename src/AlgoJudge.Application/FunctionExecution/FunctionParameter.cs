namespace AlgoJudge.Application.FunctionExecution;

public sealed class FunctionParameter
{
    public string Name { get; init; } = string.Empty;
    public FunctionValueType Type { get; init; }
}
