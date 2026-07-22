using AlgoJudge.Domain.Enums;

namespace AlgoJudge.Domain.Entities
{
    public class Problem
    {
        public int Id { get; set; }
        public string Slug { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string StatementMarkdown { get; set; } = string.Empty;
        public string ConstraintsMarkdown { get; set; } = string.Empty;
        public int TimeLimitMs { get; set; }
        public int MemoryLimitKb { get; set; }
        public DifficultyLevel Difficulty { get; set; } = DifficultyLevel.Easy;
        public ProblemExecutionMode ExecutionMode { get; set; } =
            ProblemExecutionMode.StdinStdout;
        public string? FunctionSignatureJson { get; set; }
        public string? FunctionAdapterTemplate { get; set; }
        public ProblemStatus Status { get; set; } = ProblemStatus.Draft;
        public int JudgeVersion { get; set; } = 1;
        public DateTime? PublishedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public ICollection<ProblemSample> Samples { get; set; } = new List<ProblemSample>();
        public ICollection<JudgeTestCase> JudgeTestCases { get; set; } = new List<JudgeTestCase>();
        public ICollection<ProblemTag> Tags { get; set; } = new List<ProblemTag>();
        public ICollection<Submission> Submissions { get; set; } = new List<Submission>();
        public ICollection<CodeRun> CodeRuns { get; set; } = new List<CodeRun>();
        public ICollection<ProblemAuthoringRevision> AuthoringRevisions { get; set; } =
            new List<ProblemAuthoringRevision>();
    }
}
