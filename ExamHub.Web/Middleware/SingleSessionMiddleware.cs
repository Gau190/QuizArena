using System.Security.Claims;
using ExamHub.Infrastructure.Data;
using ExamHub.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace ExamHub.Web.Middleware;

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

        var requestToken = ReadBearerToken(context);
        var presentedHash = requestToken is null
            ? context.User.FindFirstValue("token_hash")
            : JwtTokenService.HashToken(requestToken);

        var session = await db.ActiveSessions.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId);
        if (session is null || session.ExpiresAt <= DateTime.UtcNow || session.TokenHash != presentedHash)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Session expired on another device");
            return;
        }

        var extension = IsRemembered(context.User) ? TimeSpan.FromDays(14) : TimeSpan.FromMinutes(60);
        await db.ActiveSessions.Where(x => x.UserId == userId).ExecuteUpdateAsync(setters =>
            setters.SetProperty(x => x.ExpiresAt, DateTime.UtcNow.Add(extension)));

        await next(context);
    }

    private static string? ReadBearerToken(HttpContext context)
    {
        var header = context.Request.Headers.Authorization.ToString();
        return header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? header["Bearer ".Length..].Trim() : null;
    }

    private static bool IsRemembered(ClaimsPrincipal user) =>
        string.Equals(user.FindFirstValue("remember_me"), "true", StringComparison.OrdinalIgnoreCase);
}
