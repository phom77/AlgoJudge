using System;
using System.Collections.Generic;
using System.Text;

namespace AlgoJudge.Application.DTOs.TestCase
{
    public class TestCaseDto
    {
        public int Id { get; set; }
        public int ProblemId { get; set; }
        public string Input { get; set; } = string.Empty;
        public string ExpectedOutput { get; set; } = string.Empty;
        public bool IsHidden { get; set; }
    }
}
