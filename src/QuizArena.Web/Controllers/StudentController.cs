using QuizArena.Core.Enums;
using QuizArena.Core.Interfaces;
using QuizArena.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace QuizArena.Web.Controllers;

[Authorize(Roles = "Student")]
public class StudentController(QuizArenaDbContext db, IExamWorkflowService examService) : AppController
{
    [HttpGet("/thi-sinh")]
    [HttpGet("/thi-sinh/tong-quan")]
    public async Task<IActionResult> Dashboard()
    {
        var now = DateTime.UtcNow;
        var userId = CurrentUserId;

        // --- Basic class & exam assignment ---
        var studentClassIds = await db.ClassStudents.AsNoTracking()
            .Where(x => x.StudentId == userId && x.IsActive)
            .Select(x => x.ClassId)
            .ToListAsync();

        var assignedExamIds = studentClassIds.Count == 0
            ? []
            : await db.ExamClasses.AsNoTracking()
                .Where(x => studentClassIds.Contains(x.ClassId))
                .Select(x => x.ExamId)
                .Distinct()
                .ToListAsync();

        // --- All attempts of this student ---
        var allAttempts = await db.ExamAttempts.AsNoTracking()
            .Include(x => x.Exam).ThenInclude(x => x!.Subject)
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.StartedAt)
            .ToListAsync();
        ViewBag.Attempts = allAttempts;

        // --- Profile & class info ---
        ViewBag.Profile = await db.StudentProfiles.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId);

        ViewBag.MyClass = studentClassIds.Count > 0
            ? await db.Classes.AsNoTracking()
                .Include(x => x.HomeTeacher)
                .FirstOrDefaultAsync(x => studentClassIds.Contains(x.Id))
            : null;

        // --- Violation count ---
        ViewBag.ViolationCount = allAttempts.Count(x => x.Status == AttemptStatus.Flagged);

        // --- Monthly avg scores (last 6 months) using SubmittedAt ---
        var completedAttempts = allAttempts
            .Where(x => x.Score != null && x.TotalPoints is > 0 && x.SubmittedAt != null)
            .ToList();

        var monthlyScores = Enumerable.Range(0, 6)
            .Select(i => now.AddMonths(-5 + i))
            .Select(m =>
            {
                var monthScores = completedAttempts
                    .Where(x => x.SubmittedAt!.Value.Month == m.Month && x.SubmittedAt.Value.Year == m.Year)
                    .Select(x => x.Score!.Value / x.TotalPoints!.Value * 10)
                    .ToList();
                return new
                {
                    Label = m.ToString("MMM", new System.Globalization.CultureInfo("vi-VN")),
                    Month = m.Month,
                    Year = m.Year,
                    Avg = monthScores.Count > 0 ? (decimal?)Math.Round(monthScores.Average(), 1) : null
                };
            })
            .ToList<object>();
        ViewBag.MonthlyScores = monthlyScores;

        // --- Subject avg scores ---
        var subjectScores = completedAttempts
            .Where(x => x.Exam?.Subject != null)
            .GroupBy(x => x.Exam!.Subject!.Name)
            .Select(g =>
            {
                var scores = g.Select(x => x.Score!.Value / x.TotalPoints!.Value * 10).ToList();
                return new
                {
                    SubjectName = g.Key,
                    AvgScore = Math.Round(scores.Average(), 1),
                    Count = g.Count(),
                    LastDate = g.Max(x => x.SubmittedAt)
                };
            })
            .OrderByDescending(x => x.AvgScore)
            .ToList<object>();
        ViewBag.SubjectScores = subjectScores;

        // --- Class rank & class average ---
        if (studentClassIds.Count > 0)
        {
            var classStudentIds = await db.ClassStudents.AsNoTracking()
                .Where(x => studentClassIds.Contains(x.ClassId) && x.IsActive)
                .Select(x => x.StudentId)
                .ToListAsync();

            var classAvgData = await db.ExamAttempts.AsNoTracking()
                .Where(x => classStudentIds.Contains(x.UserId) && x.Score != null && x.TotalPoints > 0 && x.SubmittedAt != null)
                .GroupBy(x => x.UserId)
                .Select(g => new
                {
                    UserId = g.Key,
                    Avg = g.Average(x => x.Score!.Value / x.TotalPoints!.Value * 10)
                })
                .ToListAsync();

            var myAvg = classAvgData.FirstOrDefault(x => x.UserId == userId)?.Avg ?? 0;
            ViewBag.ClassAvg = Math.Round(classAvgData.Count > 0 ? (decimal)classAvgData.Average(x => (double)x.Avg) : 0, 1);
            ViewBag.MyAvg = Math.Round(myAvg, 1);
            ViewBag.ClassRank = classAvgData.Count(x => (double)x.Avg > (double)myAvg) + 1;
            ViewBag.ClassSize = classAvgData.Count;
        }
        else
        {
            ViewBag.ClassAvg = 0m;
            ViewBag.MyAvg = 0m;
            ViewBag.ClassRank = 0;
            ViewBag.ClassSize = 0;
        }

        // --- Available exams ---
        var exams = assignedExamIds.Count == 0
            ? []
            : await db.Exams.AsNoTracking()
                .Include(x => x.Subject)
                .Where(x => assignedExamIds.Contains(x.Id) &&
                            x.IsActive &&
                            x.Status != ExamStatus.Draft &&
                            x.Status != ExamStatus.Closed &&
                            x.StartTime <= now &&
                            x.EndTime >= now)
                .OrderBy(x => x.EndTime)
                .ToListAsync();

        return View(exams);
    }

    [HttpPost("/thi-sinh/bat-dau/{examId:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartExam(int examId)
    {
        try
        {
            var attemptId = await examService.StartAttemptAsync(examId, CurrentUserId, HttpContext.Connection.RemoteIpAddress?.ToString());
            return RedirectToAction(nameof(TakeExam), new { attemptId });
        }
        catch (InvalidOperationException ex)
        {
            TempData["StudentMessage"] = ex.Message;
            return RedirectToAction(nameof(Dashboard));
        }
    }

    [HttpGet("/thi-sinh/lam-bai/{attemptId:guid}")]
    public async Task<IActionResult> TakeExam(Guid attemptId, int index = 1)
    {
        var payload = await examService.GetQuestionAsync(attemptId, CurrentUserId, index);
        if (payload is null) return NotFound();
        ViewBag.Attempt = await db.ExamAttempts.AsNoTracking().Include(x => x.Exam).FirstAsync(x => x.Id == attemptId);
        var answeredIds = await db.ExamAttemptAnswers.AsNoTracking()
            .Where(x => x.AttemptId == attemptId && (x.AnswerIds != null || x.TextInput != null))
            .Select(x => x.QuestionId)
            .ToListAsync();
        ViewBag.AnsweredIds = answeredIds;
        ViewBag.AnsweredIndexes = await db.ExamQuestionSnapshots.AsNoTracking()
            .Where(x => x.AttemptId == attemptId && answeredIds.Contains(x.QuestionId))
            .OrderBy(x => x.OrderIndex)
            .Select(x => (int)x.OrderIndex)
            .ToListAsync();
        return View(payload);
    }

    [HttpPost("/thi-sinh/nop-bai/{attemptId:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(Guid attemptId)
    {
        await examService.SubmitAsync(attemptId, CurrentUserId);
        return RedirectToAction(nameof(Result), new { attemptId });
    }

    [HttpGet("/thi-sinh/ket-qua/{attemptId:guid}")]
    public async Task<IActionResult> Result(Guid attemptId)
    {
        var attempt = await db.ExamAttempts.AsNoTracking()
            .Include(x => x.Exam).ThenInclude(x => x!.Subject)
            .Include(x => x.Answers).ThenInclude(x => x.Question).ThenInclude(x => x!.Answers)
            .FirstOrDefaultAsync(x => x.Id == attemptId && x.UserId == CurrentUserId);
        if (attempt is null || attempt.Status == AttemptStatus.InProgress) return NotFound();

        ViewBag.AllowViewAnswer = attempt.Exam?.AllowViewAnswer ?? false;
        return View(attempt);
    }
}
