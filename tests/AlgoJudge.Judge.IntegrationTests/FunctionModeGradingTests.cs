using AlgoJudge.Application.Interfaces;
using AlgoJudge.Domain.Enums;
using AlgoJudge.Infrastructure.Grading;
using Microsoft.Extensions.Logging.Abstractions;

namespace AlgoJudge.Judge.IntegrationTests;

public sealed class FunctionModeGradingTests
{
    private const string Signature =
        "{\"className\":\"Solution\",\"methodName\":\"solve\"," +
        "\"returnType\":\"Int32\",\"parameters\":[{" +
        "\"name\":\"value\",\"type\":\"Int32\"}]}";

    private const string ExecutableAdapter = """
        #include <iostream>
        #include <iterator>
        #include <string>
        {{USER_SOURCE}}
        int main() {
            std::string input(
                (std::istreambuf_iterator<char>(std::cin)),
                std::istreambuf_iterator<char>());
            auto colon = input.find(':');
            int value = std::stoi(input.substr(colon + 1));
            {{CLASS_NAME}} solution;
            std::cout << solution.{{METHOD_NAME}}(value);
            return 0;
        }
        """;

    [Fact]
    public async Task FunctionModeBuildsHarnessBeforeSandboxCompilation()
    {
        const string userSource =
            "class Solution { public: int solve(int value) { return value * 2; } };";
        const string adapter =
            "{{USER_SOURCE}}\nint main() { {{CLASS_NAME}} value; " +
            "/* {{METHOD_NAME}} */ return 0; }";
        var sandbox = new CapturingSandbox("4");

        var outcome = await JudgeTestHarness.GradeWithSandboxAsync(
            userSource,
            "{\"value\":2}",
            "4",
            sandbox,
            NullLogger<GraderService>.Instance,
            executionMode: ProblemExecutionMode.Function,
            functionSignatureJson: Signature,
            functionAdapterTemplate: adapter);

        Assert.Equal(SubmissionStatus.Accepted, outcome.Status);
        Assert.Equal(
            $"{userSource}\nint main() {{ Solution value; /* solve */ return 0; }}",
            sandbox.CompiledSource);
        Assert.Equal("{\"value\":2}", sandbox.ReceivedInput);
    }

    [Fact]
    public async Task FunctionModeWithoutLegacyAdapterBuildsGenericHarness()
    {
        const string userSource =
            "class Solution { public: int solve(int value) { return value * 2; } };";
        var sandbox = new CapturingSandbox("4");

        var outcome = await JudgeTestHarness.GradeWithSandboxAsync(
            userSource,
            "{\"value\":2}",
            "4",
            sandbox,
            NullLogger<GraderService>.Instance,
            executionMode: ProblemExecutionMode.Function,
            functionSignatureJson: Signature);

        Assert.Equal(SubmissionStatus.Accepted, outcome.Status);
        Assert.Contains(userSource, sandbox.CompiledSource, StringComparison.Ordinal);
        Assert.Contains(
            "as_int32(root.required(\"value\"))",
            sandbox.CompiledSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "solution.solve(argument_0)",
            sandbox.CompiledSource,
            StringComparison.Ordinal);
    }

    [DockerJudgeFact]
    public async Task ValidFunctionSolutionIsAcceptedInSandbox()
    {
        const string source =
            "class Solution { public: int solve(int value) { return value * 2; } };";

        var outcome = await JudgeTestHarness.GradeWithSandboxAsync(
            source,
            "{\"value\":21}",
            "42",
            JudgeTestHarness.CreateSandbox(),
            NullLogger<GraderService>.Instance,
            executionMode: ProblemExecutionMode.Function,
            functionSignatureJson: Signature);

        Assert.Equal(SubmissionStatus.Accepted, outcome.Status);
    }

    [DockerJudgeFact]
    public async Task InvalidFunctionSolutionIsCompileErrorInSandbox()
    {
        const string invalidSource = "class Solution { this is not valid C++; };";

        var outcome = await JudgeTestHarness.GradeWithSandboxAsync(
            invalidSource,
            "{\"value\":21}",
            "42",
            JudgeTestHarness.CreateSandbox(),
            NullLogger<GraderService>.Instance,
            executionMode: ProblemExecutionMode.Function,
            functionSignatureJson: Signature);

        Assert.Equal(SubmissionStatus.CompileError, outcome.Status);
    }

    [DockerJudgeFact]
    public async Task GenericHarnessParsesAndSerializesEverySupportedValueType()
    {
        var cases = new[]
        {
            new FunctionCase(
                "Int32", "Int32",
                "class Solution { public: int solve(int value) { return value; } };",
                "{\"value\":-2147483648}", "-2147483648"),
            new FunctionCase(
                "Int64", "Int64",
                "class Solution { public: long long solve(long long value) { return value; } };",
                "{\"value\":9223372036854775807}", "9223372036854775807"),
            new FunctionCase(
                "Double", "Double",
                "class Solution { public: double solve(double value) { return value / 2; } };",
                "{\"value\":3.0}", "1.5"),
            new FunctionCase(
                "Boolean", "Boolean",
                "class Solution { public: bool solve(bool value) { return !value; } };",
                "{\"value\":true}", "false"),
            new FunctionCase(
                "String", "String",
                "class Solution { public: string solve(string value) { return value + \"\\n\\\"\"; } };",
                "{\"value\":\"xin chào\"}", "\"xin chào\\n\\\"\""),
            new FunctionCase(
                "Int32Array", "Int32Array",
                "class Solution { public: vector<int> solve(vector<int>& value) { reverse(value.begin(), value.end()); return value; } };",
                "{\"value\":[1,2,3]}", "[3,2,1]"),
            new FunctionCase(
                "Int64Array", "Int64Array",
                "class Solution { public: vector<long long> solve(vector<long long>& value) { return value; } };",
                "{\"value\":[-9223372036854775808,9223372036854775807]}",
                "[-9223372036854775808,9223372036854775807]"),
            new FunctionCase(
                "DoubleArray", "DoubleArray",
                "class Solution { public: vector<double> solve(vector<double>& value) { return value; } };",
                "{\"value\":[1.5,-2.25]}", "[1.5,-2.25]"),
            new FunctionCase(
                "BooleanArray", "BooleanArray",
                "class Solution { public: vector<bool> solve(vector<bool>& value) { for (size_t i = 0; i < value.size(); ++i) value[i] = !value[i]; return value; } };",
                "{\"value\":[true,false]}", "[false,true]"),
            new FunctionCase(
                "StringArray", "StringArray",
                "class Solution { public: vector<string> solve(vector<string>& value) { return value; } };",
                "{\"value\":[\"a\\nb\",\"\uD83D\uDE00\"]}", "[\"a\\nb\",\"😀\"]")
        };

        foreach (var functionCase in cases)
        {
            var outcome = await JudgeTestHarness.GradeWithSandboxAsync(
                functionCase.Source,
                functionCase.Input,
                functionCase.ExpectedOutput,
                JudgeTestHarness.CreateSandbox(),
                NullLogger<GraderService>.Instance,
                executionMode: ProblemExecutionMode.Function,
                functionSignatureJson: CreateSignature(
                    functionCase.ReturnType,
                    functionCase.ParameterType));

            Assert.Equal(SubmissionStatus.Accepted, outcome.Status);
        }
    }

    [DockerJudgeFact]
    public async Task GenericHarnessMapsInvalidJsonAndSolutionExceptionToRuntimeError()
    {
        const string source =
            "class Solution { public: int solve(int) { throw runtime_error(\"failure\"); } };";
        var sandbox = JudgeTestHarness.CreateSandbox();

        var invalidJson = await JudgeTestHarness.GradeWithSandboxAsync(
            source,
            "{\"unknown\":1}",
            "0",
            sandbox,
            NullLogger<GraderService>.Instance,
            executionMode: ProblemExecutionMode.Function,
            functionSignatureJson: Signature);
        var solutionException = await JudgeTestHarness.GradeWithSandboxAsync(
            source,
            "{\"value\":1}",
            "0",
            sandbox,
            NullLogger<GraderService>.Instance,
            executionMode: ProblemExecutionMode.Function,
            functionSignatureJson: Signature);

        Assert.Equal(SubmissionStatus.RuntimeError, invalidJson.Status);
        Assert.Equal(SubmissionStatus.RuntimeError, solutionException.Status);
    }

    private static string CreateSignature(string returnType, string parameterType) =>
        $$"""
        {"className":"Solution","methodName":"solve","returnType":"{{returnType}}","parameters":[{"name":"value","type":"{{parameterType}}"}]}
        """;

    private sealed record FunctionCase(
        string ReturnType,
        string ParameterType,
        string Source,
        string Input,
        string ExpectedOutput);

    private sealed class CapturingSandbox(string output) : IDockerSandbox
    {
        public string? CompiledSource { get; private set; }
        public string? ReceivedInput { get; private set; }

        public Task<SandboxCompileResult> CompileAsync(
            string sourceCode,
            string workDir,
            CancellationToken ct = default)
        {
            CompiledSource = sourceCode;
            return Task.FromResult(new SandboxCompileResult { Success = true });
        }

        public Task<SandboxRunResult> RunAsync(
            string workDir,
            string input,
            int timeLimitMs,
            int memoryLimitKb,
            CancellationToken ct = default)
        {
            ReceivedInput = input;
            return Task.FromResult(new SandboxRunResult
            {
                Status = SandboxRunStatus.Success,
                Output = output,
                ExecutionTimeMs = 1,
                MemoryUsedBytes = 1024
            });
        }
    }
}
