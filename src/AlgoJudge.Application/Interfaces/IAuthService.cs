using AlgoJudge.Application.Contracts.Auth;
using AlgoJudge.Application.Models.Auth;

namespace AlgoJudge.Application.Interfaces;

public interface IAuthService
{
    Task<AuthSessionResult> RegisterAsync(RegisterRequest request);
    Task<AuthSessionResult> LoginAsync(LoginRequest request);
    Task<AuthSessionResult> RefreshAsync(string refreshToken);
    Task RevokeAsync(string refreshToken, Guid callerId);
}
