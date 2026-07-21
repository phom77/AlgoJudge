namespace AlgoJudge.Application.FunctionExecution;

public interface IFunctionHarnessBuilder
{
    string Build(
        string sourceCode,
        FunctionSignature signature);

    string BuildLegacy(
        string sourceCode,
        FunctionSignature signature,
        string adapterTemplate);
}
