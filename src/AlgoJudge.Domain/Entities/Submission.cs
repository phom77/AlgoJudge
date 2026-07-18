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

        public int SystemTestSuiteVersion { get; set; } = 1;

        public string SourceCode { get; set; } = string.Empty;
        public string? OriginalFileName { get; set; }

        public string Language { get; set; } = "cpp"; 

        public SubmissionStatus Status { get; set; } = SubmissionStatus.Pending;
        
        public int ExecutionTime { get; set; }
       
        public int MemoryUsed { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? StartedAt { get; set; }
        public DateTime? FinishedAt { get; set; }
        public DateTime? LeaseExpiresAt { get; set; }
        public string? WorkerId { get; set; }
        public Guid? ClaimToken { get; set; }
        public int AttemptCount { get; set; }

        public User User { get; set; } = null!;
        public Problem Problem { get; set; } = null!;
    }
}
