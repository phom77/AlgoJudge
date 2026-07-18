using AlgoJudge.Application.Models.RunQueue;

namespace AlgoJudge.Application.Interfaces;

public interface IRunGraderService
{
    Task GradeAsync(RunClaim claim, CancellationToken cancellationToken = default);
}
