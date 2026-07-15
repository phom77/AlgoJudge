using AlgoJudge.Application.DTOs.Auth;
using AlgoJudge.Application.Helpers;
using AlgoJudge.Application.Interfaces;
using AlgoJudge.Domain.Entities;
using AlgoJudge.Domain.Enums;
using BCrypt.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
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
                throw new UnauthorizedAccessException("UserName hoặc Password không đúng.");

            var result = await GenerateAuthResultAsync(user);
            await _unitOfWork.SaveChangesAsync();

            return result;
        }

        public async Task<AuthResultDto> RegisterAsync(RegisterDto dto)
        {
            var existingByUserName = await _userRepository.GetByUserNameAsync(dto.UserName);
            if (existingByUserName != null)
                throw new ArgumentException("UserName đã tồn tại.");

            var existingByEmail = await _userRepository.GetByEmailAsync(dto.Email);
            if (existingByEmail != null)
                throw new ArgumentException("Email đã được sử dụng.");

            var user = new User
            {
                Id = Guid.NewGuid(),
                UserName = dto.UserName,
                Email = dto.Email,
                FullName = dto.FullName,
                Role = dto.Role,
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
                throw new UnauthorizedAccessException("Refresh token không hợp lệ.");

            if (stored.IsRevoked)
            {
                await _refreshTokenRepository.RevokeAllByUserIdAsync(stored.UserId);
                await _unitOfWork.SaveChangesAsync();
                throw new UnauthorizedAccessException(
                    "Refresh token đã bị thu hồi. Toàn bộ phiên đăng nhập đã bị đóng vì lý do bảo mật.");
            }

            if (stored.ExpiresAt <= DateTime.UtcNow)
                throw new UnauthorizedAccessException("Refresh token đã hết hạn."); ;

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
                throw new ArgumentException("Refresh token không tồn tại hoặc đã bị thu hồi.");

            if (stored.UserId != callerId)
                throw new UnauthorizedAccessException("Bạn không có quyền thu hồi token này.");

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

            var refreshTokenEntity = new RefreshToken
            {
                UserId = user.Id,
                Token = TokenHelper.Hash(refreshToken),
                ExpiresAt = DateTime.UtcNow.AddDays(refreshExpiryDays),
                CreatedAt = DateTime.UtcNow,
                IsRevoked = false
            };

            await _refreshTokenRepository.AddAsync(refreshTokenEntity);

            return new AuthResultDto
            {
                Token = accessToken,
                RefreshToken = refreshToken,
                UserName = user.UserName,
                Email = user.Email,
                Role = user.Role.ToString(),
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
                new Claim(ClaimTypes.Role, user.Role.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var expiresInHours = int.Parse(jwtSettings["ExpiresInHours"]!);
            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(expiresInHours),
                signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
