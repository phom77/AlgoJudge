using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace AlgoJudge.Application.DTOs.Submission
{
    public class CreateSubmissionDto
    {
        [Range(1, int.MaxValue, ErrorMessage = "ProblemId không hợp lệ.")]
        public int ProblemId { get; set; }

        [Required(ErrorMessage = "SourceCode là bắt buộc.")]
        public string SourceCode { get; set; } = string.Empty;
        public string Language { get; set; } = "cpp"; 
    }
}
