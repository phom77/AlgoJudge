using System;
using System.Collections.Generic;
using System.Text;

namespace AlgoJudge.Application.Interfaces
{
    /// <summary>
    /// Result of compiling code inside the sandbox
    /// </summary>
    public class SandboxCompileResult
    { 
        public bool Success { get; init; }
        public string ErrorOutput { get; init; } = string.Empty;
    }

    /// <summary>
    /// Result of running a single test case inside the sandbox
    /// </summary>
    public class SandboxRunResult
    {
        public SandboxRunStatus Status { get; init; }
        public string Output { get; init; } = string.Empty;
        public int ExecutionTimeMs { get; init; }
        public long MemoryUsedBytes { get; init; }
    }

    public enum SandboxRunStatus
    {
        Success,
        TimeLimitExceeded,
        RuntimeError,
        SystemError 
    }

    /// <summary>
    /// Abstraction for compiling and running code in an isolated environment
    /// Current implementation: Docker. Future: gVisor, nsjail,..
    /// </summary>
    public interface IDockerSandbox
    {
        /// <summary>
        /// Compile C++ source code inside a container.
        /// </summary>
        /// <param name="sourceCode">The content of the .cpp</param>
        /// <param name="workDir">The temporary directory on the host to be mounted into the container</param>
        /// <param name="ct">Cancellation token</param>
        Task<SandboxCompileResult> CompileAsync(string sourceCode, string workDir, CancellationToken ct = default);

        /// <summary>
        /// Execute the compiled binary with the given input, within time and memory limits.
        /// </summary>
        /// <param name="workDir">Same directory used in the compile step</param>
        /// <param name="input">Standard input passed to the program</param>
        /// <param name="timeLimitMs">Time limit in milliseconds</param>
        /// <param name="memoryLimitKb">Memory limit in KB, taken from Problem.MemoryLimitKb.</param>
        /// <param name="ct">Cancellation token</param>
        Task<SandboxRunResult> RunAsync(string workDir, string input, int timeLimitMs, int memoryLimitKb, CancellationToken ct = default);
    }
}
