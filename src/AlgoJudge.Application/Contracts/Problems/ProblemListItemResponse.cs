using AlgoJudge.Domain.Enums;

namespace AlgoJudge.Application.Contracts.Problems
{
    public sealed class ProblemListItemResponse
    {
        public int Id { get; init; }
        public string Slug { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public DifficultyLevel Difficulty { get; init; }
        public IReadOnlyCollection<TagResponse> Tags { get; init; } = Array.Empty<TagResponse>();
        public bool? IsSolved { get; init; }
    }
}
