using AlgoJudge.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace AlgoJudge.Application.DTOs.Submission
{
    public class SubmissionDto
    {
        public Guid Id { get; set; }
        public int ProblemId { get; set; }
        public Guid UserId { get; set; }
        public string Language { get; set; } = string.Empty;
        public SubmissionStatus Status { get; set; }
        public int ExecutionTime { get; set; }
        public int MemoryUsed { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? FinishedAt { get; set; }
    }
}
