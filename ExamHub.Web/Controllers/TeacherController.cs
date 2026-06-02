using ExamHub.Core.Entities;
using ExamHub.Core.Enums;
using ExamHub.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExamHub.Web.Controllers;

[Authorize(Roles = "Teacher")]
public class TeacherController(ExamHubDbContext db) : AppController
{
    [HttpGet("/teacher")]
    [HttpGet("/teacher/questions")]
    public async Task<IActionResult> Questions()
    {
        var questions = await db.Questions.AsNoTracking()
            .Include(x => x.Subject)
            .Include(x => x.Answers.OrderBy(a => a.OrderIndex))
            .OrderByDescending(x => x.CreatedAt)
            .Take(100)
            .ToListAsync();
        ViewBag.Subjects = await db.Subjects.AsNoTracking().Where(x => x.IsActive).ToListAsync();
        return View(questions);
    }

    [HttpPost("/teacher/questions")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateQuestion(int subjectId, string content, QuestionType type, Difficulty difficulty, decimal points, string answers, string correct)
    {
        var question = new Question { SubjectId = subjectId, Content = content, Type = type, Difficulty = difficulty, Points = points, CreatedBy = CurrentUserId };
        var rows = answers.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var correctSet = correct.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
        byte order = 1;
        foreach (var row in rows)
        {
            question.Answers.Add(new Answer { Content = row, OrderIndex = order, IsCorrect = correctSet.Contains(order.ToString()) || correctSet.Contains(row) });
            order++;
        }
        db.Questions.Add(question);
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Questions));
    }

    [HttpGet("/teacher/exams/create")]
    public async Task<IActionResult> CreateExam()
    {
        ViewBag.Subjects = await db.Subjects.AsNoTracking().Where(x => x.IsActive).ToListAsync();
        return View();
    }

    [HttpPost("/teacher/exams/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateExam(int subjectId, string title, string? description, int durationMinutes, GenerateMode generateMode, int? questionCount, decimal? totalPoints, DateTime startTime, DateTime endTime)
    {
        db.Exams.Add(new Exam
        {
            SubjectId = subjectId,
            Title = title,
            Description = description,
            DurationMinutes = durationMinutes,
            GenerateMode = generateMode,
            QuestionCount = questionCount,
            TotalPoints = totalPoints,
            StartTime = startTime.ToUniversalTime(),
            EndTime = endTime.ToUniversalTime(),
            CreatedBy = CurrentUserId
        });
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Results));
    }

    [HttpGet("/teacher/results/{examId:int?}")]
    public async Task<IActionResult> Results(int? examId)
    {
        var exams = await db.Exams.AsNoTracking().Include(x => x.Subject).OrderByDescending(x => x.StartTime).ToListAsync();
        var selectedId = examId ?? exams.FirstOrDefault()?.Id;
        ViewBag.Exams = exams;
        ViewBag.SelectedExamId = selectedId;
        var attempts = selectedId is null
            ? []
            : await db.ExamAttempts.AsNoTracking().Include(x => x.User).Where(x => x.ExamId == selectedId).OrderByDescending(x => x.Score).ToListAsync();
        return View(attempts);
    }
}
