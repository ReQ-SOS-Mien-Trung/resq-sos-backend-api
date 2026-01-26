using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using RESQ.Application.Exceptions;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Users.Dtos;
using RESQ.Domain.Entities.Users;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Collections.Generic;
using System.Text;

namespace RESQ.Infrastructure.Services
{
    public class TokenService : ITokenService
    {
        private readonly IConfiguration _config;

        public TokenService(IConfiguration config)
        {
            _config = config;
        }

        public Task<AuthResultDto> GenerateTokensAsync(UserModel user)
        {
            var access = GenerateAccessToken(user);
            var refresh = GenerateRefreshToken();
            var expiryDays = _config.GetValue<int?>("Jwt:RefreshTokenExpiryDays") ?? 7;
            var expiresAt = DateTime.UtcNow.AddDays(expiryDays);

            var result = new AuthResultDto
            {
                AccessToken = access,
                RefreshToken = refresh,
                ExpiresAt = expiresAt
            };

            return Task.FromResult(result);
        }

        public string GenerateAccessToken(UserModel user)
        {
            var key = _config.GetValue<string>("Jwt:Key");
            var issuer = _config.GetValue<string>("Jwt:Issuer");
            var audience = _config.GetValue<string>("Jwt:Audience");
            if (string.IsNullOrEmpty(key))
            {
                throw new UnprocessableEntityException("JWT signing key is not configured.");
            }
            var minutes = _config.GetValue<int?>("Jwt:AccessTokenExpiryMinutes") ?? 60;

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.Username ?? string.Empty)
            };

            if (!string.IsNullOrEmpty(user.RoleName))
            {
                claims.Add(new Claim(ClaimTypes.Role, user.RoleName));
                claims.Add(new Claim("role", user.RoleName));
            }

            var token = new JwtSecurityToken(
                issuer,
                audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(minutes),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public string GenerateRefreshToken()
        {
            var bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(64);
            return Convert.ToBase64String(bytes);
        }
    }
}
