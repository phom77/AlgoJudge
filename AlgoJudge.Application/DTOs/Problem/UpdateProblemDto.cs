using AlgoJudge.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace AlgoJudge.Application.DTOs.Problem
{
    public class UpdateProblemDto
    {
        [Required(ErrorMessage = "Title is required.")]
        [StringLength(255, MinimumLength = 3, 
            ErrorMessage = "Title must be between 3 to 255 characters.")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Description is required.")]
        public string Description { get; set; } = string.Empty;

        [Range(100, 10000,
            ErrorMessage = "TimeLimit must be between 100ms to 10000ms.")]
        public int TimeLimit { get; set; }

        [Range(16384, 1048576,
            ErrorMessage = "MemoryLimit must be between 16MB (16384KB) to 1GB (1048576KB).")]
        public int MemoryLimit { get; set; }

        public DifficultyLevel Difficulty { get; set; }
    }
}
