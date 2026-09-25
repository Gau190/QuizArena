using QuizArena.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace QuizArena.Web.Controllers;

[ApiController]
public class HealthController(QuizArenaDbContext db) : ControllerBase
{
    [HttpGet("/trang-thai")]
    [HttpGet("/healthz")] // giữ nguyên để không làm hỏng health-check/cron đã cấu hình trên Render
    public IActionResult Health()
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(new { status = "ok", utc = DateTime.UtcNow });
    }

    [HttpGet("/trang-thai/csdl")]
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

            return Ok(new { status = "ok", db = "ok", utc = DateTime.UtcNow });
        }
        catch
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { status = "db_error" });
        }
    }
}
