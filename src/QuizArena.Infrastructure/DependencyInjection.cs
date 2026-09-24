using QuizArena.Core.Interfaces;
using QuizArena.Infrastructure.Data;
using QuizArena.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace QuizArena.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddQuizArenaInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString("Default") ??
            configuration.GetConnectionString("DefaultConnection") ??
            configuration["ConnectionStrings:Default"] ??
            configuration["ConnectionStrings:DefaultConnection"] ??
            configuration["SQLCONNSTR_Default"];

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Missing database connection string. Set ConnectionStrings__Default or ConnectionStrings:Default before starting QuizArena.");
        }

        services.AddDbContext<QuizArenaDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddMemoryCache();
        services.AddScoped<JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IQuestionDrawService, QuestionDrawService>();
        services.AddScoped<IGradingService, GradingService>();
        services.AddScoped<IExamWorkflowService, ExamWorkflowService>();
        services.AddScoped<IAcademicRecordService, AcademicRecordService>();
        return services;
    }
}
