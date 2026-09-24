using QuizArena.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace QuizArena.Infrastructure.Data;

public class QuizArenaDbContextFactory : IDesignTimeDbContextFactory<QuizArenaDbContext>
{
    public QuizArenaDbContext CreateDbContext(string[] args)
    {
        DotEnv.Load();

        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Default") ??
            Environment.GetEnvironmentVariable("ConnectionStrings:Default") ??
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection") ??
            Environment.GetEnvironmentVariable("ConnectionStrings:DefaultConnection") ??
            Environment.GetEnvironmentVariable("SQLCONNSTR_Default");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Missing database connection string. Set ConnectionStrings__Default before running dotnet ef.");
        }

        var options = new DbContextOptionsBuilder<QuizArenaDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new QuizArenaDbContext(options);
    }
}
