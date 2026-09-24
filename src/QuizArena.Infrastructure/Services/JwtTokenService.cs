using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using QuizArena.Core.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace QuizArena.Infrastructure.Services;

public class JwtTokenService(IConfiguration configuration)
{
    public (string Token, string Hash, DateTime ExpiresAt) Create(User user, bool rememberMe = false)
    {
        var expiry = int.TryParse(configuration["Jwt:ExpiryMinutes"], out var parsedExpiry) ? parsedExpiry : 60;
        var expiresAt = rememberMe ? DateTime.UtcNow.AddDays(14) : DateTime.UtcNow.AddMinutes(expiry);
        var secret = string.IsNullOrWhiteSpace(configuration["Jwt:Secret"])
            ? Environment.GetEnvironmentVariable("JWT_SECRET")
            : configuration["Jwt:Secret"];
        secret ??= "dev-secret-change-me-32-characters-min";
        if (secret.Length < 32) secret = secret.PadRight(32, 'x');

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim("full_name", user.FullName),
            new Claim("remember_me", rememberMe ? "true" : "false"),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"] ?? "QuizArena",
            audience: configuration["Jwt:Audience"] ?? "QuizArenaUsers",
            claims: claims,
            expires: expiresAt,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        var raw = new JwtSecurityTokenHandler().WriteToken(token);
        return (raw, HashToken(raw), expiresAt);
    }

    public static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}
