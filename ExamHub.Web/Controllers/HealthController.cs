using ExamHub.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExamHub.Web.Controllers;

[ApiController]
public class HealthController(ExamHubDbContext db) : ControllerBase
{
    [HttpGet("/healthz")]
    public IActionResult Health()
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(new { status = "ok", utc = DateTime.UtcNow });
    }

    [HttpGet("/health/db")]
    public async Task<IActionResult> Database(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";

        try
        {
            var canConnect = await db.Database.CanConnectAsync(cancellationToken);
            if (!canConnect)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { status = "db_unavailable" });
            }

            var users = await db.Users.AsNoTracking().CountAsync(cancellationToken);
            return Ok(new { status = "ok", db = "ok", users, utc = DateTime.UtcNow });
        }
        catch
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { status = "db_error" });
        }
    }
}
