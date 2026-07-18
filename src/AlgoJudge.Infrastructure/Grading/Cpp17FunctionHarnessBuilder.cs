using AlgoJudge.Application.FunctionExecution;

namespace AlgoJudge.Infrastructure.Grading;

public sealed class Cpp17FunctionHarnessBuilder : IFunctionHarnessBuilder
{
    public string Build(
        string sourceCode,
        FunctionSignature signature,
        string adapterTemplate)
    {
        ArgumentNullException.ThrowIfNull(sourceCode);
        ArgumentNullException.ThrowIfNull(signature);
        ArgumentNullException.ThrowIfNull(adapterTemplate);

        EnsureSinglePlaceholder(adapterTemplate, FunctionHarnessPlaceholders.UserSource);
        EnsureSinglePlaceholder(adapterTemplate, FunctionHarnessPlaceholders.ClassName);
        EnsureSinglePlaceholder(adapterTemplate, FunctionHarnessPlaceholders.MethodName);

        return adapterTemplate
            .Replace(FunctionHarnessPlaceholders.ClassName, signature.ClassName, StringComparison.Ordinal)
            .Replace(FunctionHarnessPlaceholders.MethodName, signature.MethodName, StringComparison.Ordinal)
            .Replace(FunctionHarnessPlaceholders.UserSource, sourceCode, StringComparison.Ordinal);
    }

    private static void EnsureSinglePlaceholder(string template, string placeholder)
    {
        var first = template.IndexOf(placeholder, StringComparison.Ordinal);
        if (first < 0 ||
            template.IndexOf(placeholder, first + placeholder.Length, StringComparison.Ordinal) >= 0)
        {
            throw new ArgumentException(
                $"Function adapter template must contain exactly one {placeholder} placeholder.",
                nameof(template));
        }
    }
}
