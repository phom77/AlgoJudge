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
            functionSignatureJson: Signature,
            functionAdapterTemplate: ExecutableAdapter);

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
            functionSignatureJson: Signature,
            functionAdapterTemplate: ExecutableAdapter);

        Assert.Equal(SubmissionStatus.CompileError, outcome.Status);
    }

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
