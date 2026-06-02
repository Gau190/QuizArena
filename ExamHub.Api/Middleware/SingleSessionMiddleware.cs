using System.Security.Claims;
using ExamHub.Infrastructure.Data;
using ExamHub.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace ExamHub.Api.Middleware;

public class SingleSessionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ExamHubDbContext db)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        var userIdValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var header = context.Request.Headers.Authorization.ToString();
        var token = header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? header["Bearer ".Length..].Trim() : null;
        var session = await db.ActiveSessions.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId);
        if (token is null || session is null || session.ExpiresAt <= DateTime.UtcNow || session.TokenHash != JwtTokenService.HashToken(token))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Session expired on another device");
            return;
        }

        var extension = IsRemembered(context.User) ? TimeSpan.FromDays(14) : TimeSpan.FromMinutes(60);
        var newExpiresAt = DateTime.UtcNow.Add(extension);
        await db.ActiveSessions.Where(x => x.UserId == userId).ExecuteUpdateAsync(setters =>
            setters.SetProperty(x => x.ExpiresAt, newExpiresAt));

        await next(context);
    }

    private static bool IsRemembered(ClaimsPrincipal user) =>
        string.Equals(user.FindFirstValue("remember_me"), "true", StringComparison.OrdinalIgnoreCase);
}
