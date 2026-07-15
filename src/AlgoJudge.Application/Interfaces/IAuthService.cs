using AlgoJudge.Application.DTOs.Auth;
using System;
using System.Collections.Generic;
using System.Text;

namespace AlgoJudge.Application.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResultDto> RegisterAsync(RegisterDto registerDto);
        Task<AuthResultDto> LoginAsync(LoginDto loginDto);
        Task<AuthResultDto> RefreshAsync(string refreshToken);
        Task RevokeAsync(string refreshToken, Guid callerId);
    }
}
