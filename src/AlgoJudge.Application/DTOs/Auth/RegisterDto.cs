using AlgoJudge.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace AlgoJudge.Application.DTOs.Auth
{
    public class RegisterDto
    {
        [Required(ErrorMessage = "UserName là bắt buộc.")]
        [StringLength(50, MinimumLength = 3,
            ErrorMessage = "UserName phải từ 3 đến 50 ký tự.")]
        [RegularExpression(@"^[a-zA-Z0-9_]+$",
            ErrorMessage = "UserName chỉ được chứa chữ, số và dấu gạch dưới.")]
        public string UserName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email là bắt buộc.")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        [StringLength(100, ErrorMessage = "Email tối đa 100 ký tự.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password là bắt buộc.")]
        [StringLength(100, MinimumLength = 6,
            ErrorMessage = "Password phải từ 6 đến 100 ký tự.")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "FullName là bắt buộc.")]
        [StringLength(100, MinimumLength = 2,
            ErrorMessage = "FullName phải từ 2 đến 100 ký tự.")]
        public string FullName { get; set; } = string.Empty;
        public UserRole Role { get; set; } = UserRole.Student;
    }
}
