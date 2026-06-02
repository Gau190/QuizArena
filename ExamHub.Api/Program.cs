using System.Text;
using ExamHub.Api.Middleware;
using ExamHub.Infrastructure;
using ExamHub.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

if (string.IsNullOrWhiteSpace(builder.Configuration["Jwt:Secret"]))
{
    builder.Configuration["Jwt:Secret"] = Environment.GetEnvironmentVariable("JWT_SECRET");
}
builder.Services.AddExamHubInfrastructure(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddRateLimiter(opt =>
{
    opt.AddFixedWindowLimiter("login", cfg =>
    {
        cfg.PermitLimit = 5;
        cfg.Window = TimeSpan.FromMinutes(1);
        cfg.QueueLimit = 0;
    });
    opt.AddFixedWindowLimiter("api", cfg =>
    {
        cfg.PermitLimit = 60;
        cfg.Window = TimeSpan.FromMinutes(1);
    });
});

var jwtSecret = builder.Configuration["Jwt:Secret"] ?? "dev-secret-change-me-32-characters-min";
if (jwtSecret.Length < 32) jwtSecret = jwtSecret.PadRight(32, 'x');

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "ExamHub",
        ValidAudience = builder.Configuration["Jwt:Audience"] ?? "ExamHubUsers",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
    };
});
builder.Services.AddAuthorization();

var app = builder.Build();

app.Use(async (ctx, next) =>
{
    ctx.Response.Headers.Append("Content-Security-Policy",
        "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:");
    await next();
});

app.UseHttpsRedirection();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<SingleSessionMiddleware>();
app.UseAuthorization();
app.MapControllers().RequireRateLimiting("api");

if (app.Environment.IsDevelopment() || IsEnabled(app.Configuration["MIGRATE_ON_START"]))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ExamHubDbContext>();
    await db.Database.MigrateAsync();
    if (app.Environment.IsDevelopment() || IsEnabled(app.Configuration["SEED_ON_START"]))
    {
        await SeedData.EnsureSeededAsync(db);
    }
}

app.Run();

static bool IsEnabled(string? value) =>
    string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
    string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
    string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
