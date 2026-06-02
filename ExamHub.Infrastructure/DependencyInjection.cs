using ExamHub.Core.Interfaces;
using ExamHub.Infrastructure.Data;
using ExamHub.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ExamHub.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddExamHubInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ExamHubDbContext>(options =>
            options.UseSqlServer(configuration["ConnectionStrings:Default"]));

        services.AddMemoryCache();
        services.AddScoped<JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IQuestionDrawService, QuestionDrawService>();
        services.AddScoped<IGradingService, GradingService>();
        services.AddScoped<IExamWorkflowService, ExamWorkflowService>();
        return services;
    }
}
