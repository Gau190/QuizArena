using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ExamHub.Infrastructure.Data;

public class ExamHubDbContextFactory : IDesignTimeDbContextFactory<ExamHubDbContext>
{
    public ExamHubDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ExamHubDbContext>()
            .UseSqlServer("Server=localhost;Database=ExamHub;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True")
            .Options;
        return new ExamHubDbContext(options);
    }
}
