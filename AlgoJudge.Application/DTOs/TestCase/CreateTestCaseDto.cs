using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace AlgoJudge.Application.DTOs.TestCase
{
    public class CreateTestCaseDto
    {
        [Required(ErrorMessage = "Input là bắt buộc.")]
        public string Input { get; set; } = string.Empty;

        [Required(ErrorMessage = "ExpectedOutput là bắt buộc.")]
        public string ExpectedOutput { get; set; } = string.Empty;
        public bool IsHidden { get; set; } = false;
    }
}
