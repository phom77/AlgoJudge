using AlgoJudge.Application.Models.SubmissionQueue;

namespace AlgoJudge.Application.Interfaces
{
    public interface IGraderService
    {
        Task GradeAsync(
            SubmissionClaim claim,
            CancellationToken cancellationToken = default);
    }
}
