using AlgoJudge.Domain.Enums;

namespace AlgoJudge.Application.Contracts.Problems
{
    public sealed class ProblemDetailResponse
    {
        public int Id { get; init; }
        public string Slug { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string StatementMarkdown { get; init; } = string.Empty;
        public string ConstraintsMarkdown { get; init; } = string.Empty;
        public DifficultyLevel Difficulty { get; init; }
        public ProblemExecutionMode ExecutionMode { get; init; }
        public FunctionSignatureResponse? FunctionSignature { get; init; }
        public int TimeLimitMs { get; init; }
        public int MemoryLimitKb { get; init; }
        public int JudgeVersion { get; init; }
        public DateTime? PublishedAt { get; init; }
        public IReadOnlyCollection<TagResponse> Tags { get; init; } = Array.Empty<TagResponse>();
        public IReadOnlyCollection<ProblemSampleResponse> Samples { get; init; } = Array.Empty<ProblemSampleResponse>();
        public bool? IsSolved { get; init; }
    }
}
