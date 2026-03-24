using System;
using System.Collections.Generic;
using System.Text;

namespace AlgoJudge.Application.DTOs.TestCase
{
    public class CreateTestCaseDto
    {
        public string Input { get; set; } = string.Empty;
        public string ExpectedOutput { get; set; } = string.Empty;
        public bool IsHidden { get; set; } = false;
    }
}
