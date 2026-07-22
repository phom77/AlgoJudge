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

        var harness = builder.BuildLegacy(source, signature, template);

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
            builder.BuildLegacy("source", signature, "{{USER_SOURCE}} {{CLASS_NAME}}"));

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

        var harness = builder.BuildLegacy(source, signature, template);

        Assert.Equal(
            $"{source}\nSolution instance; // solve",
            harness);
    }

    [Fact]
    public void BuildGeneratesParserInvocationAndSerializerFromSignature()
    {
        const string source =
            "class Solution { public: vector<string> solve(int value, vector<string>& names) { return names; } };";
        var signature = new FunctionSignature
        {
            ClassName = "Solution",
            MethodName = "solve",
            ReturnType = FunctionValueType.StringArray,
            Parameters =
            [
                new FunctionParameter { Name = "value", Type = FunctionValueType.Int32 },
                new FunctionParameter { Name = "names", Type = FunctionValueType.StringArray }
            ]
        };

        var harness = new Cpp17FunctionHarnessBuilder().Build(source, signature);

        Assert.Contains(source, harness, StringComparison.Ordinal);
        Assert.Contains("root.require_object_size(2);", harness, StringComparison.Ordinal);
        Assert.Contains("as_int32(root.required(\"value\"))", harness, StringComparison.Ordinal);
        Assert.Contains("as_string_array(root.required(\"names\"))", harness, StringComparison.Ordinal);
        Assert.Contains("solution.solve(argument_0, argument_1)", harness, StringComparison.Ordinal);
        Assert.Contains("algojudge_harness::serialize(result)", harness, StringComparison.Ordinal);
        Assert.DoesNotContain("{{USER_SOURCE}}", harness, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("class")]
    [InlineData("not-valid")]
    [InlineData("")]
    public void BuildRejectsInvalidSignatureIdentifiers(string methodName)
    {
        var signature = new FunctionSignature
        {
            ClassName = "Solution",
            MethodName = methodName,
            ReturnType = FunctionValueType.Int32
        };

        var exception = Assert.Throws<ArgumentException>(() =>
            new Cpp17FunctionHarnessBuilder().Build("class Solution {};", signature));

        Assert.Contains("identifier", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildRejectsDuplicateParameterNames()
    {
        var signature = new FunctionSignature
        {
            ClassName = "Solution",
            MethodName = "solve",
            ReturnType = FunctionValueType.Int32,
            Parameters =
            [
                new FunctionParameter { Name = "value", Type = FunctionValueType.Int32 },
                new FunctionParameter { Name = "value", Type = FunctionValueType.Int64 }
            ]
        };

        var exception = Assert.Throws<ArgumentException>(() =>
            new Cpp17FunctionHarnessBuilder().Build("class Solution {};", signature));

        Assert.Contains("Duplicate", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildLegacyTemplateCreatesAllCompatibilityPlaceholders()
    {
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

        var template = builder.BuildLegacyTemplate(signature);
        var harness = builder.BuildLegacy(
            "class Solution { public: int solve(int value) { return value; } };",
            signature,
            template);

        Assert.DoesNotContain("{{", harness, StringComparison.Ordinal);
        Assert.Contains("solution.solve(argument_0)", harness, StringComparison.Ordinal);
    }
}
