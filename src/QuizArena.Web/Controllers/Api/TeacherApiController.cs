using System.Security.Claims;
using QuizArena.Core.Enums;
using QuizArena.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace QuizArena.Web.Controllers.Api;

[ApiController]
[Route("api/v1/teacher")]
[Authorize(Roles = "Admin,Teacher")]
public class TeacherApiController(QuizArenaDbContext db) : ControllerBase
{
    [HttpGet("stats/{examId:int}")]
    public async Task<IActionResult> Stats(int examId)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var canAccess = await db.Exams.AsNoTracking()
            .AnyAsync(x => x.Id == examId && (User.IsInRole(nameof(UserRole.Admin)) || x.CreatedBy == userId));
        if (!canAccess) return Forbid();

        var attempts = await db.ExamAttempts.AsNoTracking().Where(x => x.ExamId == examId && x.Score != null).ToListAsync();
        var total = attempts.Count;
        var average = total == 0 ? 0 : attempts.Average(x => x.Score ?? 0);
        var pass = attempts.Count(x => (x.Score ?? 0) >= 5);
        return Ok(new
        {
            total,
            average,
            passRate = total == 0 ? 0 : Math.Round(pass * 100m / total, 2),
            highest = total == 0 ? 0 : attempts.Max(x => x.Score) ?? 0,
            lowest = total == 0 ? 0 : attempts.Min(x => x.Score) ?? 0
        });
    }
}
