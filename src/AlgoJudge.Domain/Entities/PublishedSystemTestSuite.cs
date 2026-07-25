using AlgoJudge.Domain.Enums;

namespace AlgoJudge.Domain.Entities;

public sealed class PublishedSystemTestSuite
{
    public int ProblemId { get; set; }
    public int Version { get; set; }
    public OutputCheckerKind OutputCheckerKind { get; set; } = OutputCheckerKind.TokenExact;
    public double? AbsoluteTolerance { get; set; }
    public double? RelativeTolerance { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Problem Problem { get; set; } = null!;
    public ICollection<JudgeTestCase> TestCases { get; set; } = new List<JudgeTestCase>();
}
