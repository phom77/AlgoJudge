namespace AlgoJudge.Application.Models.Auth;

public sealed record AuthSessionResult(
    string AccessToken,
    string RefreshToken,
    string UserName,
    string Email,
    bool IsAdmin,
    DateTime AccessTokenExpiresAt,
    DateTime RefreshTokenExpiresAt);
