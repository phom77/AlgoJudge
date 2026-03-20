using System;
using System.Collections.Generic;
using System.Text;

namespace AlgoJudge.Application.DTOs.Submission
{
    public class CreateSubmissionDto
    {
        public int ProblemId { get; set; }
        public string SourceCode { get; set; } = string.Empty;
        public string Language { get; set; } = "cpp"; 
        public Guid UserId { get; set; }
    }
}
