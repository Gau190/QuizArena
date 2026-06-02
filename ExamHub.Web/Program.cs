using System.Text;
using ExamHub.Infrastructure;
using ExamHub.Infrastructure.Data;
using ExamHub.Web.Middleware;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

if (string.IsNullOrWhiteSpace(builder.Configuration["Jwt:Secret"]))
{
    builder.Configuration["Jwt:Secret"] = Environment.GetEnvironmentVariable("JWT_SECRET");
}

builder.Services.AddExamHubInfrastructure(builder.Configuration);
builder.Services.AddControllersWithViews();
builder.Services.AddAntiforgery();
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

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/account/login";
        options.LogoutPath = "/account/logout";
        options.AccessDeniedPath = "/account/login";
        var expiry = int.TryParse(builder.Configuration["Jwt:ExpiryMinutes"], out var parsedExpiry) ? parsedExpiry : 60;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(expiry);
    })
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
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

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.Use(async (ctx, next) =>
{
    ctx.Response.Headers.Append("Content-Security-Policy",
        "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; img-src 'self' data:; font-src 'self' https://cdn.jsdelivr.net data:");
    await next();
});

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<SingleSessionMiddleware>();
app.UseAuthorization();

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

app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

static bool IsEnabled(string? value) =>
    string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
    string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
    string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
