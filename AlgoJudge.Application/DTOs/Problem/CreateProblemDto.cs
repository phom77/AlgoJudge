using AlgoJudge.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace AlgoJudge.Application.DTOs.Problem
{
    public class CreateProblemDto
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int TimeLimit { get; set; } = 1000;
        public int MemoryLimit { get; set; } = 262144;
        public DifficultyLevel Difficulty { get; set; } = DifficultyLevel.Easy;
        public Guid CreatedBy { get; set; }
    }
}
