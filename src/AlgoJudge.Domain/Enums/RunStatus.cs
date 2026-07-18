namespace AlgoJudge.Domain.Enums;

public enum RunStatus
{
    Pending = 0,
    Completed = 1,
    Running = 2,
    TimeLimitExceeded = 3,
    MemoryLimitExceeded = 4,
    CompileError = 5,
    RuntimeError = 6
}
