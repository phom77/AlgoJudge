using AlgoJudge.Application.Interfaces;

namespace AlgoJudge.Backend.EndToEndTests;

internal sealed class CountingDockerSandbox : IDockerSandbox
{
    private readonly IDockerSandbox _inner;
    private int _compileCount;
    private int _runCount;

    public CountingDockerSandbox(IDockerSandbox inner)
    {
        _inner = inner;
    }

    public int CompileCount => Volatile.Read(ref _compileCount);
    public int RunCount => Volatile.Read(ref _runCount);

    public Task<SandboxCompileResult> CompileAsync(
        string sourceCode,
        string workDir,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _compileCount);
        return _inner.CompileAsync(sourceCode, workDir, cancellationToken);
    }

    public Task<SandboxRunResult> RunAsync(
        string workDir,
        string input,
        int timeLimitMs,
        int memoryLimitKb,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _runCount);
        return _inner.RunAsync(
            workDir,
            input,
            timeLimitMs,
            memoryLimitKb,
            cancellationToken);
    }
}
