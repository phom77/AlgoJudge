namespace AlgoJudge.Application.Models.Auth;

public sealed record AuthSessionResult(
    string AccessToken,
    string RefreshToken,
    string UserName,
    string Email,
    DateTime AccessTokenExpiresAt,
    DateTime RefreshTokenExpiresAt);
