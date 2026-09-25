using Microsoft.Extensions.Configuration;

namespace QuizArena.Infrastructure.Services;

public static class JwtSecret
{
    private const string DevFallback = "dev-secret-change-me-32-characters-min";

    /// <summary>Lấy khoá JWT. Ngoài môi trường Development, thiếu khoá hoặc khoá ngắn hơn 32 ký tự sẽ báo lỗi ngay khi khởi động.</summary>
    public static string Resolve(IConfiguration configuration)
    {
        var secret = configuration["Jwt:Secret"];
        if (string.IsNullOrWhiteSpace(secret)) secret = Environment.GetEnvironmentVariable("JWT_SECRET");

        if (string.IsNullOrWhiteSpace(secret) || secret.Length < 32)
        {
            var isDevelopment = string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Development", StringComparison.OrdinalIgnoreCase);
            if (!isDevelopment)
            {
                throw new InvalidOperationException("Thiếu JWT_SECRET hoặc khoá ngắn hơn 32 ký tự. Hãy đặt biến môi trường JWT_SECRET.");
            }

            secret = (string.IsNullOrWhiteSpace(secret) ? DevFallback : secret).PadRight(32, 'x');
        }

        return secret;
    }
}
