using System;
using System.Collections.Generic;
using System.Text;

namespace AlgoJudge.Domain.Entities
{
    public class Problem
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int TimeLimit { get; set; }
        public int MemoryLimit { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public ICollection<TestCase> TestCases { get; set; } = new List<TestCase>();
        public ICollection<Submission> Submissions { get; set; } = new List<Submission>();
    }
}
