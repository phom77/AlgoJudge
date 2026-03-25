using AlgoJudge.Application.DTOs.Auth;
using AlgoJudge.Application.Interfaces;
using AlgoJudge.Domain.Entities;
using AlgoJudge.Domain.Enums;
using BCrypt.Net;
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
        private readonly IUnitOfWork _unitOfWork;
        private readonly IConfiguration _configuration;

        public AuthService(
            IUserRepository userRepository,
            IUnitOfWork unitOfWork,
            IConfiguration configuration)
        {
            _userRepository = userRepository;
            _unitOfWork = unitOfWork;
            _configuration = configuration;
        }

        public async Task<AuthResultDto> LoginAsync(LoginDto dto)
        {
            var user = await _userRepository.GetByUserNameAsync(dto.UserName);

            if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
                throw new UnauthorizedAccessException("UserName hoặc Password không đúng.");

            return GenerateAuthResult(user);
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
            await _unitOfWork.SaveChangesAsync();

            return GenerateAuthResult(user);
        }

        private AuthResultDto GenerateAuthResult(User user)
        {
            var token = GenerateJwtToken(user);
            var expiresAt = DateTime.UtcNow.AddHours(
                int.Parse(_configuration["Jwt:ExpiresInHours"]!));

            return new AuthResultDto
            {
                Token = token,
                UserName = user.UserName,
                Email = user.Email,
                Role = user.Role.ToString(),
                ExpiresAt = expiresAt
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
