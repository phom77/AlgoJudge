using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel.DataAnnotations;

namespace AlgoJudge.Application.DTOs.Auth
{
    public class RefreshRequestDto
    {
        [Required(ErrorMessage = "Must have RefreshToken")]
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class RevokeRequestDto
    {
        [Required(ErrorMessage = "Must have RefreshToken")]
        public string RefreshToken { get; set; } = string.Empty;
    }
}
