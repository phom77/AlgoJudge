using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace AlgoJudge.Application.DTOs.Auth
{
    public class LoginDto
    {
        [Required(ErrorMessage = "UserName là bắt buộc.")]
        public string UserName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password là bắt buộc.")]
        public string Password { get; set; } = string.Empty;
    }
}
