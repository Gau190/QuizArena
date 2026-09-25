using Microsoft.Extensions.Configuration;
using QuizArena.Infrastructure.Services;

namespace QuizArena.Tests;

[Collection("env")]
public class JwtSecretTests
{
    private static IConfiguration Config(string? secret) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Secret"] = secret }).Build();

    private static T WithEnvironment<T>(string environment, Func<T> action)
    {
        var oldEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        var oldSecret = Environment.GetEnvironmentVariable("JWT_SECRET");
        try
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", environment);
            Environment.SetEnvironmentVariable("JWT_SECRET", null);
            return action();
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", oldEnv);
            Environment.SetEnvironmentVariable("JWT_SECRET", oldSecret);
        }
    }

    [Fact]
    public void Production_without_secret_throws() =>
        Assert.Throws<InvalidOperationException>(() => WithEnvironment("Production", () => JwtSecret.Resolve(Config(null))));

    [Fact]
    public void Production_with_short_secret_throws() =>
        Assert.Throws<InvalidOperationException>(() => WithEnvironment("Production", () => JwtSecret.Resolve(Config("short"))));

    [Fact]
    public void Production_with_long_secret_returns_it()
    {
        var secret = new string('k', 40);
        Assert.Equal(secret, WithEnvironment("Production", () => JwtSecret.Resolve(Config(secret))));
    }

    [Fact]
    public void Development_without_secret_uses_padded_fallback() =>
        Assert.True(WithEnvironment("Development", () => JwtSecret.Resolve(Config(null))).Length >= 32);
}
