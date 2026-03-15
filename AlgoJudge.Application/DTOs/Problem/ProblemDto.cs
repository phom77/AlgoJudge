using AlgoJudge.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace AlgoJudge.Application.DTOs.Problem
{
    public class ProblemDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int TimeLimit { get; set; }
        public int MemoryLimit { get; set; }
        public DifficultyLevel Difficulty { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
