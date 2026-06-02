using ExamHub.Core.Enums;
using ExamHub.Core.Interfaces;
using ExamHub.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExamHub.Web.Controllers;

[Authorize(Roles = "Student")]
public class StudentController(ExamHubDbContext db, IExamWorkflowService examService) : AppController
{
    [HttpGet("/student")]
    [HttpGet("/student/dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        var now = DateTime.UtcNow;
        ViewBag.Attempts = await db.ExamAttempts.AsNoTracking()
            .Include(x => x.Exam).ThenInclude(x => x!.Subject)
            .Where(x => x.UserId == CurrentUserId)
            .OrderByDescending(x => x.StartedAt)
            .ToListAsync();
        var exams = await db.Exams.AsNoTracking()
            .Include(x => x.Subject)
            .Where(x => x.IsActive && x.StartTime <= now && x.EndTime >= now)
            .OrderBy(x => x.EndTime)
            .ToListAsync();
        return View(exams);
    }

    [HttpPost("/student/start/{examId:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartExam(int examId)
    {
        var attemptId = await examService.StartAttemptAsync(examId, CurrentUserId, HttpContext.Connection.RemoteIpAddress?.ToString());
        return RedirectToAction(nameof(TakeExam), new { attemptId });
    }

    [HttpGet("/student/exam/{attemptId:guid}")]
    public async Task<IActionResult> TakeExam(Guid attemptId, int index = 1)
    {
        var payload = await examService.GetQuestionAsync(attemptId, CurrentUserId, index);
        if (payload is null) return NotFound();
        ViewBag.Attempt = await db.ExamAttempts.AsNoTracking().Include(x => x.Exam).FirstAsync(x => x.Id == attemptId);
        ViewBag.AnsweredIds = await db.ExamAttemptAnswers.AsNoTracking().Where(x => x.AttemptId == attemptId && (x.AnswerIds != null || x.TextInput != null)).Select(x => x.QuestionId).ToListAsync();
        return View(payload);
    }

    [HttpPost("/student/submit/{attemptId:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(Guid attemptId)
    {
        await examService.SubmitAsync(attemptId, CurrentUserId);
        return RedirectToAction(nameof(Result), new { attemptId });
    }

    [HttpGet("/student/result/{attemptId:guid}")]
    public async Task<IActionResult> Result(Guid attemptId)
    {
        var attempt = await db.ExamAttempts.AsNoTracking()
            .Include(x => x.Exam).ThenInclude(x => x!.Subject)
            .Include(x => x.Answers).ThenInclude(x => x.Question).ThenInclude(x => x!.Answers)
            .FirstOrDefaultAsync(x => x.Id == attemptId && x.UserId == CurrentUserId);
        return attempt is null || attempt.Status == AttemptStatus.InProgress ? NotFound() : View(attempt);
    }
}
