using AlgoJudge.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace AlgoJudge.Application.Contracts.Problems
{
    public sealed class ProblemListQuery
    {
        [MaxLength(100, ErrorMessage = "Search must not exceed 100 characters.")]
        public string? Search { get; set; }

        public DifficultyLevel? Difficulty { get; set; }

        [MaxLength(10, ErrorMessage = "No more than 10 tags may be supplied.")]
        public string[] Tags { get; set; } = Array.Empty<string>();

        public bool? Solved { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Page number must be at least 1.")]
        public int PageNumber { get; set; } = 1;

        [Range(1, 100, ErrorMessage = "Page size must be between 1 and 100.")]
        public int PageSize { get; set; } = 20;
    }
}
