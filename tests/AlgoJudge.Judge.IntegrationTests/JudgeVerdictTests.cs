using AlgoJudge.Domain.Enums;

namespace AlgoJudge.Judge.IntegrationTests;

[Collection(DockerJudgeCollection.Name)]
public sealed class JudgeVerdictTests
{
    private const string SumSolution = """
        #include <iostream>
        int main() {
            long long left = 0, right = 0;
            std::cin >> left >> right;
            std::cout << left + right << '\n';
            return 0;
        }
        """;

    [DockerJudgeFact]
    public async Task CorrectSolutionIsAcceptedWithMeasuredTimeAndMemory()
    {
        var outcome = await JudgeTestHarness.GradeAsync(
            SumSolution,
            input: "20 22\n",
            expectedOutput: "42\n");

        Assert.Equal(SubmissionStatus.Accepted, outcome.Status);
        Assert.InRange(outcome.ExecutionTimeMs, 1, 250);
        Assert.True(outcome.MemoryUsedKb > 0);
    }

    [DockerJudgeFact]
    public async Task IncorrectOutputIsWrongAnswer()
    {
        var outcome = await JudgeTestHarness.GradeAsync(
            """
            #include <iostream>
            int main() { std::cout << 41 << '\n'; }
            """,
            input: string.Empty,
            expectedOutput: "42\n");

        Assert.Equal(SubmissionStatus.WrongAnswer, outcome.Status);
    }

    [DockerJudgeFact]
    public async Task InfiniteLoopIsTimeLimitExceeded()
    {
        var outcome = await JudgeTestHarness.GradeAsync(
            """
            int main() {
                volatile unsigned long long value = 0;
                while (true) { value++; }
            }
            """,
            input: string.Empty,
            expectedOutput: string.Empty,
            timeLimitMs: 200);

        Assert.Equal(SubmissionStatus.TimeLimitExceeded, outcome.Status);
        Assert.InRange(outcome.ExecutionTimeMs, 150, 1_000);
    }

    [DockerJudgeFact]
    public async Task ExcessiveAllocationIsMemoryLimitExceeded()
    {
        var outcome = await JudgeTestHarness.GradeAsync(
            """
            #include <cstddef>
            #include <cstdlib>
            int main() {
                constexpr std::size_t bytes = 256ULL * 1024ULL * 1024ULL;
                volatile unsigned char* memory =
                    static_cast<volatile unsigned char*>(std::malloc(bytes));
                if (memory == nullptr) {
                    while (true) { }
                }
                for (std::size_t index = 0; index < bytes; index += 4096) {
                    memory[index] = 1;
                }
                return memory[0];
            }
            """,
            input: string.Empty,
            expectedOutput: string.Empty,
            timeLimitMs: 5_000,
            memoryLimitKb: 64 * 1024);

        Assert.Equal(SubmissionStatus.MemoryLimitExceeded, outcome.Status);
        Assert.True(outcome.MemoryUsedKb > 0);
    }

    [DockerJudgeFact]
    public async Task InvalidSourceIsCompileError()
    {
        var outcome = await JudgeTestHarness.GradeAsync(
            "int main( { this is not valid C++; }",
            input: string.Empty,
            expectedOutput: string.Empty);

        Assert.Equal(SubmissionStatus.CompileError, outcome.Status);
    }

    [DockerJudgeFact]
    public async Task AbortedProcessIsRuntimeError()
    {
        var outcome = await JudgeTestHarness.GradeAsync(
            """
            #include <cstdlib>
            int main() { std::abort(); }
            """,
            input: string.Empty,
            expectedOutput: string.Empty);

        Assert.Equal(SubmissionStatus.RuntimeError, outcome.Status);
    }
}
