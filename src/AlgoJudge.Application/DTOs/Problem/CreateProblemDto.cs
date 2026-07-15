using AlgoJudge.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace AlgoJudge.Application.DTOs.Problem
{
    public class CreateProblemDto
    {
        [Required(ErrorMessage = "Title là bắt buộc.")]
        [StringLength(255, MinimumLength = 3,
            ErrorMessage = "Title phải từ 3 đến 255 ký tự.")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Description là bắt buộc.")]
        public string Description { get; set; } = string.Empty;

        [Range(1, 1000, ErrorMessage = "Score must be between 1 to 1000.")]
        public int Score { get; set; } = 100;

        [Range(100, 10000,
            ErrorMessage = "TimeLimit phải từ 100ms đến 10000ms.")]
        public int TimeLimit { get; set; } = 1000;

        [Range(16384, 1048576,
            ErrorMessage = "MemoryLimit phải từ 16MB (16384KB) đến 1GB (1048576KB).")]
        public int MemoryLimit { get; set; } = 262144;
        public DifficultyLevel Difficulty { get; set; } = DifficultyLevel.Easy;
    }
}
