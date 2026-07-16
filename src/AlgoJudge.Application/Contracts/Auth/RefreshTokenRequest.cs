using System.ComponentModel.DataAnnotations;

namespace AlgoJudge.Application.Contracts.Auth;

public sealed class RefreshTokenRequest
{
    [Required(ErrorMessage = "Refresh token is required.")]
    public string RefreshToken { get; set; } = string.Empty;
}
