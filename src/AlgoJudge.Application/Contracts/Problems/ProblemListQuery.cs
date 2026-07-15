using AlgoJudge.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace AlgoJudge.Application.Contracts.Problems
{
    public sealed class ProblemListQuery
    {
        [MaxLength(100)]
        public string? Search { get; set; }

        public DifficultyLevel? Difficulty { get; set; }

        [MaxLength(10)]
        public string[] Tags { get; set; } = Array.Empty<string>();

        public bool? Solved { get; set; }

        [Range(1, int.MaxValue)]
        public int PageNumber { get; set; } = 1;

        [Range(1, 100)]
        public int PageSize { get; set; } = 20;
    }
}
