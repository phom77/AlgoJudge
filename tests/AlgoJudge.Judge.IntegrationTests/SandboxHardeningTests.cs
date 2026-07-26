using AlgoJudge.Application.Interfaces;

namespace AlgoJudge.Judge.IntegrationTests;

[Collection(DockerJudgeCollection.Name)]
public sealed class SandboxHardeningTests
{
    [DockerJudgeFact]
    public async Task RuntimeIsNonRootCapabilityFreeNetworklessAndReadOnly()
    {
        var sandbox = JudgeTestHarness.CreateSandbox();
        var workDirectory = CreateWorkDirectory();

        try
        {
            var compileResult = await sandbox.CompileAsync(
                """
                #include <fstream>
                #include <iostream>
                #include <string>
                #include <unistd.h>

                std::string readValue(const char* path) {
                    std::ifstream file(path);
                    std::string value;
                    file >> value;
                    return value;
                }

                int main() {
                    std::ifstream status("/proc/self/status");
                    std::string line;
                    std::string capabilities;
                    while (std::getline(status, line)) {
                        if (line.rfind("CapEff:", 0) == 0) {
                            capabilities = line.substr(line.find_first_not_of(" \t", 7));
                        }
                    }

                    std::ifstream network("/proc/net/dev");
                    int nonLoopbackInterfaces = 0;
                    while (std::getline(network, line)) {
                        const auto separator = line.find(':');
                        if (separator == std::string::npos) continue;
                        auto name = line.substr(0, separator);
                        name.erase(0, name.find_first_not_of(" \t"));
                        if (name != "lo") nonLoopbackInterfaces++;
                    }

                    std::ofstream rootWrite("/algojudge-forbidden");
                    std::ofstream artifactWrite("/artifact/forbidden");
                    std::ifstream sourceVisible("/artifact/solution.cpp");
                    auto pidsLimit = readValue("/sys/fs/cgroup/pids.max");
                    if (pidsLimit.empty()) {
                        pidsLimit = readValue("/sys/fs/cgroup/pids/pids.max");
                    }

                    auto swapLimit = readValue("/sys/fs/cgroup/memory.swap.max");
                    bool swapDisabled = swapLimit == "0";
                    if (swapLimit.empty()) {
                        const auto memoryLimit = readValue(
                            "/sys/fs/cgroup/memory/memory.limit_in_bytes");
                        const auto memoryAndSwapLimit = readValue(
                            "/sys/fs/cgroup/memory/memory.memsw.limit_in_bytes");
                        swapDisabled = !memoryLimit.empty()
                            && memoryLimit == memoryAndSwapLimit;
                    }

                    std::cout << getuid() << ' '
                              << capabilities << ' '
                              << rootWrite.is_open() << ' '
                              << artifactWrite.is_open() << ' '
                              << sourceVisible.is_open() << ' '
                              << nonLoopbackInterfaces << ' '
                              << pidsLimit << ' '
                              << swapDisabled;
                }
                """,
                workDirectory);
            Assert.True(compileResult.Success, compileResult.ErrorOutput);

            var runResult = await sandbox.RunAsync(
                workDirectory,
                input: string.Empty,
                timeLimitMs: 1_000,
                memoryLimitKb: 64 * 1024);

            Assert.Equal(SandboxRunStatus.Success, runResult.Status);
            Assert.Equal(
                "10001 0000000000000000 0 0 0 0 32 1",
                runResult.Output.Trim());
        }
        finally
        {
            TryDeleteDirectory(workDirectory);
        }
    }

    [DockerJudgeFact]
    public async Task StdoutAndStderrAreIndependentlyBounded()
    {
        const int outputLimitBytes = 1_024;
        var sandbox = JudgeTestHarness.CreateSandbox(
            stdoutLimitBytes: outputLimitBytes,
            stderrLimitBytes: outputLimitBytes);
        var workDirectory = CreateWorkDirectory();

        try
        {
            var compileResult = await sandbox.CompileAsync(
                """
                #include <iostream>
                int main() {
                    char stream = 'o';
                    std::cin >> stream;
                    for (int index = 0; index < 100000; index++) {
                        if (stream == 'e') std::cerr.put('e');
                        else std::cout.put('o');
                    }
                }
                """,
                workDirectory);
            Assert.True(compileResult.Success, compileResult.ErrorOutput);

            var stdoutResult = await sandbox.RunAsync(
                workDirectory,
                input: "o\n",
                timeLimitMs: 1_000,
                memoryLimitKb: 64 * 1024);
            var stderrResult = await sandbox.RunAsync(
                workDirectory,
                input: "e\n",
                timeLimitMs: 1_000,
                memoryLimitKb: 64 * 1024);

            Assert.Equal(SandboxRunStatus.OutputLimitExceeded, stdoutResult.Status);
            Assert.Equal(outputLimitBytes, stdoutResult.Output.Length);
            Assert.Empty(stdoutResult.ErrorOutput);
            Assert.Equal(SandboxRunStatus.OutputLimitExceeded, stderrResult.Status);
            Assert.Empty(stderrResult.Output);
            Assert.Equal(outputLimitBytes, stderrResult.ErrorOutput.Length);
        }
        finally
        {
            TryDeleteDirectory(workDirectory);
        }
    }

    [DockerJudgeFact]
    public async Task BatchExecutionUsesFreshMeasuredProcessForEveryTestcase()
    {
        var sandbox = JudgeTestHarness.CreateSandbox();
        var workDirectory = CreateWorkDirectory();

        try
        {
            var compileResult = await sandbox.CompileAsync(
                """
                #include <iostream>
                int invocationCount = 0;
                int main() {
                    int value = 0;
                    std::cin >> value;
                    std::cout << ++invocationCount << ':' << value * 2;
                }
                """,
                workDirectory);
            Assert.True(compileResult.Success, compileResult.ErrorOutput);

            var inputs = Enumerable.Range(1, 200)
                .Select(value => $"{value}\n")
                .ToArray();
            var results = await sandbox.RunBatchAsync(
                workDirectory,
                inputs,
                timeLimitMs: 1_000,
                memoryLimitKb: 64 * 1024);

            Assert.Equal(inputs.Length, results.Count);
            for (var index = 0; index < results.Count; index++)
            {
                Assert.Equal(SandboxRunStatus.Success, results[index].Status);
                Assert.Equal($"1:{(index + 1) * 2}", results[index].Output);
                Assert.InRange(results[index].ExecutionTimeMs, 1, 1_000);
                Assert.True(results[index].MemoryUsedBytes > 0);
            }
        }
        finally
        {
            TryDeleteDirectory(workDirectory);
        }
    }

    [DockerJudgeFact]
    public async Task BatchExecutionStopsAfterFirstSandboxFailure()
    {
        var sandbox = JudgeTestHarness.CreateSandbox();
        var workDirectory = CreateWorkDirectory();

        try
        {
            var compileResult = await sandbox.CompileAsync(
                """
                #include <iostream>
                int main() {
                    int value = 0;
                    std::cin >> value;
                    if (value == 1) {
                        for (;;) {
                        }
                    }
                    std::cout << value;
                }
                """,
                workDirectory);
            Assert.True(compileResult.Success, compileResult.ErrorOutput);

            var results = await sandbox.RunBatchAsync(
                workDirectory,
                ["1\n", "2\n", "3\n"],
                timeLimitMs: 100,
                memoryLimitKb: 64 * 1024);

            var result = Assert.Single(results);
            Assert.Equal(SandboxRunStatus.TimeLimitExceeded, result.Status);
        }
        finally
        {
            TryDeleteDirectory(workDirectory);
        }
    }

    [DockerJudgeFact]
    public async Task ContinuingBatchExecutionReturnsEverySandboxFailureAndSuccess()
    {
        var sandbox = JudgeTestHarness.CreateSandbox();
        var workDirectory = CreateWorkDirectory();

        try
        {
            var compileResult = await sandbox.CompileAsync(
                """
                #include <cstdlib>
                #include <iostream>
                int main() {
                    int value = 0;
                    std::cin >> value;
                    if (value == 1) std::abort();
                    std::cout << value * 2;
                }
                """,
                workDirectory);
            Assert.True(compileResult.Success, compileResult.ErrorOutput);

            var results = await sandbox.RunBatchContinuingAfterFailureAsync(
                workDirectory,
                ["1\n", "2\n", "3\n"],
                timeLimitMs: 1_000,
                memoryLimitKb: 64 * 1024);

            Assert.Equal(3, results.Count);
            Assert.Equal(SandboxRunStatus.RuntimeError, results[0].Status);
            Assert.Equal(SandboxRunStatus.Success, results[1].Status);
            Assert.Equal("4", results[1].Output);
            Assert.Equal(SandboxRunStatus.Success, results[2].Status);
            Assert.Equal("6", results[2].Output);
        }
        finally
        {
            TryDeleteDirectory(workDirectory);
        }
    }

    [DockerJudgeFact]
    public async Task BatchExecutionPreservesEmptyMultilineAndUtf8Inputs()
    {
        var sandbox = JudgeTestHarness.CreateSandbox();
        var workDirectory = CreateWorkDirectory();

        try
        {
            var compileResult = await sandbox.CompileAsync(
                """
                #include <iostream>
                int main() {
                    std::cout << std::cin.rdbuf();
                }
                """,
                workDirectory);
            Assert.True(compileResult.Success, compileResult.ErrorOutput);

            string[] inputs =
            [
                string.Empty,
                "first line\nsecond line\n",
                "xin chào 🚀\n"
            ];
            var results = await sandbox.RunBatchAsync(
                workDirectory,
                inputs,
                timeLimitMs: 1_000,
                memoryLimitKb: 64 * 1024);

            Assert.Equal(inputs.Length, results.Count);
            for (var index = 0; index < inputs.Length; index++)
            {
                Assert.Equal(SandboxRunStatus.Success, results[index].Status);
                Assert.Equal(inputs[index], results[index].Output);
            }
        }
        finally
        {
            TryDeleteDirectory(workDirectory);
        }
    }

    private static string CreateWorkDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "algojudge-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // The sandbox already cleans its containers; leave filesystem cleanup best-effort.
        }
    }
}
