using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace AlgoJudge.Application.Helpers
{
    public static class TokenHelper
    {
        public static string Hash(string token)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        public static string GenerateRefreshToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(64);
            return Convert.ToBase64String(bytes);
        }
    }
}
