using Backend.Models;
using Backend.Extensions;
using Microsoft.IdentityModel.Tokens;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Backend.Services
{
    public class TokenService
    {
        private readonly IConfiguration _config;
        public TokenService(IConfiguration config) => _config = config;

        public string GenerateAccessToken(ApplicationUser user)
        {
            var jwtSettings = _config.GetSection("Jwt");
            var jwtKey = jwtSettings["Key"]
                ?? throw new InvalidOperationException("Jwt:Key must be configured.");
            var jwtIssuer = jwtSettings["Issuer"]
                ?? throw new InvalidOperationException("Jwt:Issuer must be configured.");
            var jwtAudience = jwtSettings["Audience"]
                ?? throw new InvalidOperationException("Jwt:Audience must be configured.");
            if (!double.TryParse(jwtSettings["AccessTokenExpiryMinutes"], NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var expiryMinutes) || expiryMinutes <= 0)
                throw new InvalidOperationException("Jwt:AccessTokenExpiryMinutes must be a positive number.");

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.UserName ?? ""),
                new(AuthClaimTypes.Role, user.Role.ToString()),
                new(AuthClaimTypes.FullName, $"{user.FirstName} {user.LastName}" ?? user.UserName ?? ""),
                new(AuthClaimTypes.ScopeLevel, user.ScopeLevel.ToString()),
                new(AuthClaimTypes.ScopeProvinceId, user.ScopeProvinceId?.ToString() ?? ""),
                new(AuthClaimTypes.ScopeCityId, user.ScopeCityId?.ToString() ?? ""),
            };

            var token = new JwtSecurityToken(
                issuer: jwtIssuer,
                audience: jwtAudience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public string GenerateRefreshToken()
        {
            var randomBytes = new byte[64];
            using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);
            return Convert.ToBase64String(randomBytes);
        }
    }
}