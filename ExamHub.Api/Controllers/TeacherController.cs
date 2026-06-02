using ExamHub.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExamHub.Api.Controllers;

[ApiController]
[Route("api/teacher")]
[Authorize(Roles = "Teacher")]
public class TeacherController(ExamHubDbContext db) : ControllerBase
{
    [HttpGet("stats/{examId:int}")]
    public async Task<IActionResult> Stats(int examId)
    {
        var attempts = await db.ExamAttempts.AsNoTracking().Where(x => x.ExamId == examId && x.Score != null).ToListAsync();
        var total = attempts.Count;
        var average = total == 0 ? 0 : attempts.Average(x => x.Score ?? 0);
        var pass = attempts.Count(x => (x.Score ?? 0) >= 5);
        return Ok(new { total, average, passRate = total == 0 ? 0 : Math.Round(pass * 100m / total, 2), highest = attempts.Max(x => x.Score) ?? 0, lowest = attempts.Min(x => x.Score) ?? 0 });
    }
}
