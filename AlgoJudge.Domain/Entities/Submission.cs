using AlgoJudge.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace AlgoJudge.Domain.Entities
{
    public class Submission
    {
        public Guid Id { get; set; }

        public Guid UserId { get; set; }

        public int ProblemId { get; set; }

        public string SourceCode { get; set; } = string.Empty;
        public string? OriginalFileName { get; set; }

        public string Language { get; set; } = "C++"; 

        public SubmissionStatus Status { get; set; } = SubmissionStatus.Pending;
        
        public int ExecutionTime { get; set; }
       
        public int MemoryUsed { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public User User { get; set; } = null!;
        public Problem Problem { get; set; } = null!;
    }
}
