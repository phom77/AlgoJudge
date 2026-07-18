using AlgoJudge.Application.FunctionExecution;
using AlgoJudge.Infrastructure.Grading;

namespace AlgoJudge.ContentTool.Tests;

public sealed class Cpp17FunctionHarnessBuilderTests
{
    [Fact]
    public void BuildReplacesContractPlaceholdersExactlyOnce()
    {
        const string source = "class Solution { public: int solve(int value) { return value; } };";
        const string template = "{{USER_SOURCE}}\n{{CLASS_NAME}} instance; // {{METHOD_NAME}}";
        var signature = new FunctionSignature
        {
            ClassName = "Solution",
            MethodName = "solve",
            ReturnType = FunctionValueType.Int32,
            Parameters =
            [
                new FunctionParameter { Name = "value", Type = FunctionValueType.Int32 }
            ]
        };
        var builder = new Cpp17FunctionHarnessBuilder();

        var harness = builder.Build(source, signature, template);

        Assert.Equal($"{source}\nSolution instance; // solve", harness);
    }

    [Fact]
    public void BuildRejectsMissingPlaceholder()
    {
        var builder = new Cpp17FunctionHarnessBuilder();
        var signature = new FunctionSignature
        {
            ClassName = "Solution",
            MethodName = "solve"
        };

        var exception = Assert.Throws<ArgumentException>(() =>
            builder.Build("source", signature, "{{USER_SOURCE}} {{CLASS_NAME}}"));

        Assert.Contains("{{METHOD_NAME}}", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildDoesNotInterpretPlaceholderTextInsideSubmittedSource()
    {
        const string source = "// {{CLASS_NAME}} {{METHOD_NAME}} {{USER_SOURCE}}";
        const string template = "{{USER_SOURCE}}\n{{CLASS_NAME}} instance; // {{METHOD_NAME}}";
        var signature = new FunctionSignature
        {
            ClassName = "Solution",
            MethodName = "solve"
        };
        var builder = new Cpp17FunctionHarnessBuilder();

        var harness = builder.Build(source, signature, template);

        Assert.Equal(
            $"{source}\nSolution instance; // solve",
            harness);
    }
}
