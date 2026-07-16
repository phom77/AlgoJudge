namespace AlgoJudge.Application.Contracts.Auth;

public sealed class AuthResponse
{
    public string UserName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }
}
