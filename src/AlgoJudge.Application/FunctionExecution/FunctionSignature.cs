namespace AlgoJudge.Application.FunctionExecution;

public sealed class FunctionSignature
{
    public string ClassName { get; init; } = string.Empty;
    public string MethodName { get; init; } = string.Empty;
    public FunctionValueType ReturnType { get; init; }
    public IReadOnlyList<FunctionParameter> Parameters { get; init; } = [];
}
