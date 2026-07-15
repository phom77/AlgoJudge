using AlgoJudge.Application.DTOs.Auth;
using AlgoJudge.Application.Helpers;
using AlgoJudge.Application.Interfaces;
using AlgoJudge.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace AlgoJudge.Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IRefreshTokenRepository _refreshTokenRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IConfiguration _configuration;

        public AuthService(
            IUserRepository userRepository,
            IRefreshTokenRepository refreshTokenRepository,
            IUnitOfWork unitOfWork,
            IConfiguration configuration)
        {
            _userRepository = userRepository;
            _refreshTokenRepository = refreshTokenRepository;
            _unitOfWork = unitOfWork;
            _configuration = configuration;
        }

        public async Task<AuthResultDto> LoginAsync(LoginDto dto)
        {
            var user = await _userRepository.GetByUserNameAsync(dto.UserName);

            if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
                throw new UnauthorizedAccessException("Username or password is incorrect.");

            var result = await GenerateAuthResultAsync(user);
            await _unitOfWork.SaveChangesAsync();
            return result;
        }

        public async Task<AuthResultDto> RegisterAsync(RegisterDto dto)
        {
            if (await _userRepository.GetByUserNameAsync(dto.UserName) != null)
                throw new ArgumentException("Username is already taken.");

            if (await _userRepository.GetByEmailAsync(dto.Email) != null)
                throw new ArgumentException("Email is already in use.");

            var user = new User
            {
                Id = Guid.NewGuid(),
                UserName = dto.UserName,
                Email = dto.Email,
                FullName = dto.FullName,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                CreatedAt = DateTime.UtcNow
            };

            await _userRepository.AddAsync(user);

            var result = await GenerateAuthResultAsync(user);
            await _unitOfWork.SaveChangesAsync();
            return result;
        }

        public async Task<AuthResultDto> RefreshAsync(string refreshToken)
        {
            var hash = TokenHelper.Hash(refreshToken);
            var stored = await _refreshTokenRepository.GetByTokenAsync(hash);

            if (stored == null)
                throw new UnauthorizedAccessException("Refresh token is invalid.");

            if (stored.IsRevoked)
            {
                await _refreshTokenRepository.RevokeAllByUserIdAsync(stored.UserId);
                await _unitOfWork.SaveChangesAsync();
                throw new UnauthorizedAccessException(
                    "Refresh token has been revoked. All active sessions were closed for security.");
            }

            if (stored.ExpiresAt <= DateTime.UtcNow)
                throw new UnauthorizedAccessException("Refresh token has expired.");

            stored.IsRevoked = true;

            var result = await GenerateAuthResultAsync(stored.User);
            await _unitOfWork.SaveChangesAsync();
            return result;
        }

        public async Task RevokeAsync(string refreshToken, Guid callerId)
        {
            var hash = TokenHelper.Hash(refreshToken);
            var stored = await _refreshTokenRepository.GetByTokenAsync(hash);

            if (stored == null || stored.IsRevoked)
                throw new ArgumentException("Refresh token does not exist or has been revoked.");

            if (stored.UserId != callerId)
                throw new UnauthorizedAccessException(
                    "You are not allowed to revoke this refresh token.");

            stored.IsRevoked = true;
            await _unitOfWork.SaveChangesAsync();
        }

        private async Task<AuthResultDto> GenerateAuthResultAsync(User user)
        {
            var accessToken = GenerateJwtToken(user);
            var refreshToken = TokenHelper.GenerateRefreshToken();
            var expiresInHours = int.Parse(_configuration["Jwt:ExpiresInHours"]!);
            var refreshExpiryDays = int.Parse(
                _configuration["Jwt:RefreshTokenExpiryDays"] ?? "7");

            await _refreshTokenRepository.AddAsync(new RefreshToken
            {
                UserId = user.Id,
                Token = TokenHelper.Hash(refreshToken),
                ExpiresAt = DateTime.UtcNow.AddDays(refreshExpiryDays),
                CreatedAt = DateTime.UtcNow,
                IsRevoked = false
            });

            return new AuthResultDto
            {
                Token = accessToken,
                RefreshToken = refreshToken,
                UserName = user.UserName,
                Email = user.Email,
                ExpiresAt = DateTime.UtcNow.AddHours(expiresInHours)
            };
        }

        private string GenerateJwtToken(User user)
        {
            var jwtSettings = _configuration.GetSection("Jwt");
            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!));

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var expiresInHours = int.Parse(jwtSettings["ExpiresInHours"]!);
            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(expiresInHours),
                signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
