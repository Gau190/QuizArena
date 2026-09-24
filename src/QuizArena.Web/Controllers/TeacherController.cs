using QuizArena.Core.Entities;
using QuizArena.Core.Enums;
using QuizArena.Infrastructure.Data;
using QuizArena.Web.Models;
using QuizArena.Web.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Xml.Linq;

namespace QuizArena.Web.Controllers;

[Authorize(Roles = "Admin,Teacher")]
public class TeacherController(QuizArenaDbContext db) : AppController
{
    [HttpGet("/giang-vien")]
    [HttpGet("/giang-vien/ngan-hang-cau-hoi")]
    public async Task<IActionResult> Questions(int? subjectId, QuestionType? type, Difficulty? difficulty, string? q)
    {
        var query = db.Questions.AsNoTracking()
            .Include(x => x.Subject)
            .Include(x => x.Answers.OrderBy(a => a.OrderIndex))
            .AsQueryable();
        if (subjectId.HasValue) query = query.Where(x => x.SubjectId == subjectId.Value);
        if (type.HasValue) query = query.Where(x => x.Type == type.Value);
        if (difficulty.HasValue) query = query.Where(x => x.Difficulty == difficulty.Value);
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(x => x.Content.Contains(q));

        var questions = await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(100)
            .ToListAsync();
        ViewBag.Subjects = await db.Subjects.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync();
        ViewBag.SubjectId = subjectId;
        ViewBag.Type = type;
        ViewBag.Difficulty = difficulty;
        ViewBag.Query = q;
        ViewBag.CurrentUserId = CurrentUserId;
        return View(questions);
    }

    [HttpPost("/giang-vien/ngan-hang-cau-hoi")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateQuestion(int subjectId, string content, QuestionType type, Difficulty difficulty, decimal points, string answers, string correct, string? explanation = null)
    {
        var question = BuildQuestion(subjectId, content, type, difficulty, points, answers, correct, CurrentUserId, out var error, explanation);
        if (question is null)
        {
            TempData["TeacherMessage"] = error;
            return RedirectToAction(nameof(Questions));
        }

        db.Questions.Add(question);
        await db.SaveChangesAsync();
        TempData["TeacherMessage"] = "Đã thêm câu hỏi vào ngân hàng.";
        return RedirectToAction(nameof(Questions));
    }

    [HttpPost("/giang-vien/ngan-hang-cau-hoi/nhap")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportQuestions(IFormFile file, int fallbackSubjectId)
    {
        if (file.Length == 0)
        {
            TempData["TeacherMessage"] = "File import không có dữ liệu.";
            return RedirectToAction(nameof(Questions));
        }

        var subjects = await db.Subjects.AsNoTracking().Where(x => x.IsActive).ToListAsync();
        var subjectIds = subjects.Select(x => x.Id).ToHashSet();
        if (!subjectIds.Contains(fallbackSubjectId))
        {
            TempData["TeacherMessage"] = "Môn mặc định không hợp lệ.";
            return RedirectToAction(nameof(Questions));
        }

        var rows = Path.GetExtension(file.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase)
            ? await ReadXlsxRowsAsync(file)
            : await ReadCsvRowsAsync(file);
        if (rows.Count <= 1)
        {
            TempData["TeacherMessage"] = "File import cần có dòng tiêu đề và ít nhất một câu hỏi.";
            return RedirectToAction(nameof(Questions));
        }

        var header = rows[0].Select(NormalizeHeader).ToList();
        var subjectByName = subjects.ToDictionary(x => NormalizeHeader(x.Name), x => x.Id);
        var imported = 0;
        var skipped = 0;
        foreach (var row in rows.Skip(1))
        {
            var values = header
                .Select((name, index) => new { name, value = index < row.Count ? row[index] : "" })
                .ToDictionary(x => x.name, x => x.value, StringComparer.OrdinalIgnoreCase);

            if (!TryGet(values, "content", out var content) || string.IsNullOrWhiteSpace(content))
            {
                skipped++;
                continue;
            }

            var subjectId = fallbackSubjectId;
            if (TryGet(values, "subjectid", out var subjectIdText) && int.TryParse(subjectIdText, out var parsedSubjectId) && subjectIds.Contains(parsedSubjectId))
            {
                subjectId = parsedSubjectId;
            }
            else if (TryGet(values, "subject", out var subjectName) && subjectByName.TryGetValue(NormalizeHeader(subjectName), out var namedSubjectId))
            {
                subjectId = namedSubjectId;
            }

            var type = TryGet(values, "type", out var typeText) && Enum.TryParse<QuestionType>(typeText, true, out var parsedType)
                ? parsedType
                : QuestionType.SingleChoice;
            var difficulty = TryGet(values, "difficulty", out var difficultyText) && Enum.TryParse<Difficulty>(difficultyText, true, out var parsedDifficulty)
                ? parsedDifficulty
                : Difficulty.Medium;
            var points = TryGet(values, "points", out var pointsText) && decimal.TryParse(pointsText, out var parsedPoints)
                ? parsedPoints
                : 1m;
            var answerText = TryGet(values, "answers", out var importedAnswers) ? importedAnswers.Replace("|", Environment.NewLine) : "";
            var correctText = TryGet(values, "correct", out var importedCorrect) ? importedCorrect : "";
            TryGet(values, "explanation", out var importedExplanation);

            var question = BuildQuestion(subjectId, content, type, difficulty, points, answerText, correctText, CurrentUserId, out _, importedExplanation);
            if (question is null)
            {
                skipped++;
                continue;
            }

            db.Questions.Add(question);
            imported++;
        }

        await db.SaveChangesAsync();
        TempData["TeacherMessage"] = $"Đã import {imported} câu hỏi. Bỏ qua {skipped} dòng không hợp lệ.";
        return RedirectToAction(nameof(Questions));
    }

    [HttpPost("/giang-vien/ngan-hang-cau-hoi/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateQuestion(int id, int subjectId, string content, QuestionType type, Difficulty difficulty, decimal points, string answers, string correct, string? explanation = null)
    {
        var question = await db.Questions.Include(x => x.Answers).FirstOrDefaultAsync(x => x.Id == id);
        if (question is null) return NotFound();
        if (!CanManage(question.CreatedBy)) return Forbid();
        if (await IsQuestionUsedAsync(id))
        {
            TempData["TeacherMessage"] = $"Không thể sửa Q{id} vì đã xuất hiện trong lượt thi. Hãy tắt câu hỏi và tạo phiên bản mới.";
            return RedirectToAction(nameof(Questions));
        }

        var replacement = BuildQuestion(subjectId, content, type, difficulty, points, answers, correct, question.CreatedBy, out var error, explanation);
        if (replacement is null)
        {
            TempData["TeacherMessage"] = error;
            return RedirectToAction(nameof(Questions));
        }

        question.SubjectId = replacement.SubjectId;
        question.Content = replacement.Content;
        question.Type = replacement.Type;
        question.Difficulty = replacement.Difficulty;
        question.Points = replacement.Points;
        question.Explanation = replacement.Explanation;

        db.Answers.RemoveRange(question.Answers);
        foreach (var answer in replacement.Answers) question.Answers.Add(answer);

        await db.SaveChangesAsync();
        TempData["TeacherMessage"] = $"Đã cập nhật câu hỏi Q{id}.";
        return RedirectToAction(nameof(Questions));
    }

    [HttpPost("/giang-vien/ngan-hang-cau-hoi/{id:int}/bat-tat")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleQuestion(int id)
    {
        var question = await db.Questions.FindAsync(id);
        if (question is null) return NotFound();
        if (!CanManage(question.CreatedBy)) return Forbid();
        question.IsActive = !question.IsActive;
        await db.SaveChangesAsync();
        TempData["TeacherMessage"] = question.IsActive ? $"Đã bật Q{id}." : $"Đã tắt Q{id}.";
        return RedirectToAction(nameof(Questions));
    }

    [HttpPost("/giang-vien/ngan-hang-cau-hoi/{id:int}/xoa")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteQuestion(int id)
    {
        var question = await db.Questions.Include(x => x.Answers).FirstOrDefaultAsync(x => x.Id == id);
        if (question is null) return NotFound();
        if (!CanManage(question.CreatedBy)) return Forbid();

        var used = await IsQuestionUsedAsync(id);
        if (used)
        {
            TempData["TeacherMessage"] = $"Không thể xóa Q{id} vì đã xuất hiện trong lượt thi. Hãy tắt câu hỏi nếu không dùng nữa.";
            return RedirectToAction(nameof(Questions));
        }

        db.Answers.RemoveRange(question.Answers);
        db.Questions.Remove(question);
        await db.SaveChangesAsync();
        TempData["TeacherMessage"] = $"Đã xóa Q{id}.";
        return RedirectToAction(nameof(Questions));
    }

    [HttpGet("/giang-vien/ky-thi/tao")]
    public async Task<IActionResult> CreateExam()
    {
        ViewBag.Subjects = await db.Subjects.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync();
        ViewBag.Classes = await db.Classes.AsNoTracking().OrderBy(x => x.Name).ToListAsync();
        ViewBag.Exams = await db.Exams.AsNoTracking()
            .Include(x => x.Subject)
            .Include(x => x.Classes).ThenInclude(x => x.Class)
            .Where(x => User.IsInRole(nameof(UserRole.Admin)) || x.CreatedBy == CurrentUserId)
            .OrderByDescending(x => x.StartTime)
            .Take(50)
            .ToListAsync();
        return View();
    }

    [HttpPost("/giang-vien/ky-thi/tao")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateExam(
        int subjectId,
        int[] classIds,
        string title,
        string? description,
        int durationMinutes,
        int maxAttempts,
        GenerateMode generateMode,
        int? questionCount,
        decimal? totalPoints,
        int easyCount,
        int mediumCount,
        int hardCount,
        decimal passScore,
        DateTime startTime,
        DateTime endTime,
        bool shuffleQuestions,
        bool shuffleAnswers,
        bool antiCheat,
        bool autoSubmit,
        bool allowViewAnswer,
        string publishMode = "Published")
    {
        if (!ValidateExamInput(title, generateMode, questionCount, totalPoints, easyCount, mediumCount, hardCount, startTime, endTime, out var error))
        {
            TempData["TeacherMessage"] = error;
            return RedirectToAction(nameof(CreateExam));
        }

        var exam = new Exam
        {
            SubjectId = subjectId,
            Title = title.Trim(),
            Description = description?.Trim(),
            DurationMinutes = Math.Max(1, durationMinutes),
            MaxAttempts = Math.Max(1, maxAttempts),
            GenerateMode = generateMode,
            QuestionCount = generateMode == GenerateMode.ByCount ? questionCount : null,
            TotalPoints = generateMode == GenerateMode.ByPoints ? totalPoints : null,
            EasyCount = generateMode == GenerateMode.ByCount ? Math.Max(0, easyCount) : 0,
            MediumCount = generateMode == GenerateMode.ByCount ? Math.Max(0, mediumCount) : 0,
            HardCount = generateMode == GenerateMode.ByCount ? Math.Max(0, hardCount) : 0,
            PassScore = Math.Clamp(passScore, 0m, 10m),
            ShuffleQuestions = shuffleQuestions,
            ShuffleAnswers = shuffleAnswers,
            AntiCheat = antiCheat,
            AutoSubmit = autoSubmit,
            AllowViewAnswer = allowViewAnswer,
            Status = publishMode == "Draft" ? ExamStatus.Draft : ExamStatus.Published,
            IsActive = publishMode != "Draft",
            StartTime = startTime.ToUniversalTime(),
            EndTime = endTime.ToUniversalTime(),
            CreatedBy = CurrentUserId
        };
        db.Exams.Add(exam);
        await db.SaveChangesAsync();
        await SetExamClassesAsync(exam.Id, classIds);
        TempData["TeacherMessage"] = $"Đã tạo kỳ thi {title}.";
        return RedirectToAction(nameof(CreateExam));
    }

    [HttpPost("/giang-vien/ky-thi/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateExam(
        int id,
        int subjectId,
        int[] classIds,
        string title,
        string? description,
        int durationMinutes,
        int maxAttempts,
        GenerateMode generateMode,
        int? questionCount,
        decimal? totalPoints,
        int easyCount,
        int mediumCount,
        int hardCount,
        decimal passScore,
        ExamStatus status,
        DateTime startTime,
        DateTime endTime,
        bool shuffleQuestions,
        bool shuffleAnswers,
        bool antiCheat,
        bool autoSubmit,
        bool allowViewAnswer)
    {
        var exam = await db.Exams.FindAsync(id);
        if (exam is null) return NotFound();
        if (!User.IsInRole(nameof(UserRole.Admin)) && exam.CreatedBy != CurrentUserId) return Forbid();
        if (!ValidateExamInput(title, generateMode, questionCount, totalPoints, easyCount, mediumCount, hardCount, startTime, endTime, out var error))
        {
            TempData["TeacherMessage"] = error;
            return RedirectToAction(nameof(CreateExam));
        }

        exam.SubjectId = subjectId;
        exam.Title = title.Trim();
        exam.Description = description?.Trim();
        exam.DurationMinutes = Math.Max(1, durationMinutes);
        exam.MaxAttempts = Math.Max(1, maxAttempts);
        exam.GenerateMode = generateMode;
        exam.QuestionCount = generateMode == GenerateMode.ByCount ? questionCount : null;
        exam.TotalPoints = generateMode == GenerateMode.ByPoints ? totalPoints : null;
        exam.EasyCount = generateMode == GenerateMode.ByCount ? Math.Max(0, easyCount) : 0;
        exam.MediumCount = generateMode == GenerateMode.ByCount ? Math.Max(0, mediumCount) : 0;
        exam.HardCount = generateMode == GenerateMode.ByCount ? Math.Max(0, hardCount) : 0;
        exam.PassScore = Math.Clamp(passScore, 0m, 10m);
        exam.ShuffleQuestions = shuffleQuestions;
        exam.ShuffleAnswers = shuffleAnswers;
        exam.AntiCheat = antiCheat;
        exam.AutoSubmit = autoSubmit;
        exam.AllowViewAnswer = allowViewAnswer;
        exam.Status = status;
        exam.IsActive = status != ExamStatus.Draft && status != ExamStatus.Closed;
        exam.StartTime = startTime.ToUniversalTime();
        exam.EndTime = endTime.ToUniversalTime();
        exam.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await SetExamClassesAsync(exam.Id, classIds);
        TempData["TeacherMessage"] = $"Đã cập nhật kỳ thi {exam.Title}.";
        return RedirectToAction(nameof(CreateExam));
    }

    [HttpPost("/giang-vien/ky-thi/{id:int}/bat-tat")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleExam(int id)
    {
        var exam = await db.Exams.FindAsync(id);
        if (exam is null) return NotFound();
        if (!User.IsInRole(nameof(UserRole.Admin)) && exam.CreatedBy != CurrentUserId) return Forbid();

        exam.IsActive = !exam.IsActive;
        exam.Status = exam.IsActive
            ? exam.Status == ExamStatus.Draft || exam.Status == ExamStatus.Closed ? ExamStatus.Published : exam.Status
            : ExamStatus.Closed;
        exam.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        TempData["TeacherMessage"] = exam.IsActive ? $"Đã mở kỳ thi {exam.Title}." : $"Đã tắt kỳ thi {exam.Title}.";
        return RedirectToAction(nameof(CreateExam));
    }

    [HttpPost("/giang-vien/ky-thi/{id:int}/xoa")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteExam(int id)
    {
        var exam = await db.Exams.FindAsync(id);
        if (exam is null) return NotFound();
        if (!User.IsInRole(nameof(UserRole.Admin)) && exam.CreatedBy != CurrentUserId) return Forbid();
        if (await db.ExamAttempts.AnyAsync(x => x.ExamId == id))
        {
            TempData["TeacherMessage"] = $"Không thể xóa {exam.Title} vì đã có lượt thi. Hãy tắt kỳ thi nếu không dùng nữa.";
            return RedirectToAction(nameof(CreateExam));
        }

        var rooms = await db.ExamRooms.Where(x => x.ExamId == id).ToListAsync();
        foreach (var room in rooms)
        {
            room.ExamId = null;
            room.ExamName = $"{room.ExamName} (đã xóa)";
        }
        db.Exams.Remove(exam);
        await db.SaveChangesAsync();
        TempData["TeacherMessage"] = $"Đã xóa kỳ thi {exam.Title}.";
        return RedirectToAction(nameof(Results));
    }

    [HttpGet("/giang-vien/ket-qua/{examId:int?}")]
    public async Task<IActionResult> Results(int? examId, AttemptStatus? status)
    {
        var exams = await db.Exams.AsNoTracking()
            .Include(x => x.Subject)
            .Where(x => User.IsInRole(nameof(UserRole.Admin)) || x.CreatedBy == CurrentUserId)
            .OrderByDescending(x => x.StartTime)
            .ToListAsync();
        var selectedId = examId ?? exams.FirstOrDefault()?.Id;
        if (examId.HasValue && exams.All(x => x.Id != examId.Value)) return Forbid();
        ViewBag.Exams = exams;
        ViewBag.SelectedExamId = selectedId;
        ViewBag.Status = status;
        var attempts = selectedId is null
            ? []
            : await db.ExamAttempts.AsNoTracking()
                .Include(x => x.User)
                .Where(x => x.ExamId == selectedId && (!status.HasValue || x.Status == status.Value))
                .OrderByDescending(x => x.Score)
                .ToListAsync();
        ViewBag.QuestionStats = selectedId is null
            ? new List<QuestionResultStat>()
            : await BuildQuestionStatsAsync(selectedId.Value, status);
        return View(attempts);
    }

    [HttpGet("/giang-vien/ngan-hang-cau-hoi/cau-hoi-yeu")]
    public async Task<IActionResult> WeakQuestions()
    {
        var rows = await db.ExamAttemptAnswers.AsNoTracking()
            .Include(x => x.Question).ThenInclude(x => x!.Subject)
            .Where(x => x.IsCorrect.HasValue &&
                        x.Question != null &&
                        (User.IsInRole(nameof(UserRole.Admin)) || x.Question.CreatedBy == CurrentUserId))
            .ToListAsync();

        var model = rows
            .GroupBy(x => new
            {
                x.QuestionId,
                x.Question!.Content,
                Subject = x.Question.Subject!.Name,
                x.Question.ReportCount
            })
            .Select(group =>
            {
                var total = group.Count();
                var correct = group.Count(x => x.IsCorrect == true);
                var rate = total == 0 ? 0 : correct * 100 / total;
                return new WeakQuestionRow(group.Key.QuestionId, WebUtility.HtmlDecode(group.Key.Subject), group.Key.Content, total, correct, rate, group.Key.ReportCount);
            })
            .Where(x => x.Total > 0 && x.CorrectRate < 60)
            .OrderBy(x => x.CorrectRate)
            .ThenByDescending(x => x.Total)
            .Take(50)
            .ToList();

        return View(model);
    }

    [HttpGet("/giang-vien/ket-qua/{examId:int}/xuat")]
    public async Task<IActionResult> ExportResults(int examId, AttemptStatus? status)
    {
        var exam = await db.Exams.AsNoTracking().Include(x => x.Subject).FirstOrDefaultAsync(x => x.Id == examId);
        if (exam is null) return NotFound();
        if (!User.IsInRole(nameof(UserRole.Admin)) && exam.CreatedBy != CurrentUserId) return Forbid();

        var attempts = await db.ExamAttempts.AsNoTracking()
            .Include(x => x.User)
            .Where(x => x.ExamId == examId && (!status.HasValue || x.Status == status.Value))
            .OrderByDescending(x => x.Score)
            .ToListAsync();
        var rows = attempts.Select(x => new[]
        {
            x.User?.FullName ?? "",
            x.User?.Username ?? "",
            exam.Title,
            exam.Subject?.Name ?? "",
            x.Status.ToString(),
            x.Score?.ToString("0.##") ?? "",
            x.SubmittedAt?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "",
            x.Notes ?? ""
        });
        var bytes = SpreadsheetExporter.CreateXlsx(
            "Ket qua",
            ["Student", "Username", "Exam", "Subject", "Status", "Score", "SubmittedAt", "Notes"],
            rows);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"ket-qua-{exam.Id}.xlsx");
    }

    private bool CanManage(Guid ownerId) => User.IsInRole(nameof(UserRole.Admin)) || ownerId == CurrentUserId;

    private async Task SetExamClassesAsync(int examId, int[] classIds)
    {
        var existing = await db.ExamClasses.Where(x => x.ExamId == examId).ToListAsync();
        db.ExamClasses.RemoveRange(existing);

        var uniqueClassIds = classIds.Distinct().ToList();
        var validClassIds = await db.Classes.AsNoTracking()
            .Where(x => uniqueClassIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync();
        foreach (var classId in validClassIds)
        {
            db.ExamClasses.Add(new ExamClass { ExamId = examId, ClassId = classId });
        }

        await db.SaveChangesAsync();
    }

    private async Task<bool> IsQuestionUsedAsync(int id)
    {
        return await db.ExamAttemptAnswers.AnyAsync(x => x.QuestionId == id) ||
               await db.ExamQuestionSnapshots.AnyAsync(x => x.QuestionId == id);
    }

    private async Task<List<QuestionResultStat>> BuildQuestionStatsAsync(int examId, AttemptStatus? status)
    {
        var rows = await db.ExamAttemptAnswers.AsNoTracking()
            .Include(x => x.Attempt)
            .Include(x => x.Question)
            .Where(x => x.Attempt != null &&
                        x.Attempt.ExamId == examId &&
                        x.IsCorrect.HasValue &&
                        (!status.HasValue || x.Attempt.Status == status.Value))
            .ToListAsync();

        return rows
            .Where(x => x.Question is not null)
            .GroupBy(x => new { x.QuestionId, x.Question!.Content, x.Question.Type })
            .Select(group => new QuestionResultStat(
                group.Key.QuestionId,
                group.Key.Content,
                group.Key.Type,
                group.Count(),
                group.Count(x => x.IsCorrect == true)))
            .OrderBy(x => x.QuestionId)
            .ToList();
    }

    private static Question? BuildQuestion(int subjectId, string content, QuestionType type, Difficulty difficulty, decimal points, string answers, string correct, Guid createdBy, out string error, string? explanation = null)
    {
        error = "";
        var rows = answers.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (string.IsNullOrWhiteSpace(content) || rows.Length == 0)
        {
            error = "Câu hỏi cần có nội dung và ít nhất một đáp án.";
            return null;
        }

        var correctSet = correct.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (correctSet.Count == 0)
        {
            error = "Cần chỉ định ít nhất một đáp án đúng.";
            return null;
        }

        var question = new Question
        {
            SubjectId = subjectId,
            Content = content.Trim(),
            Type = type,
            Difficulty = difficulty,
            Points = Math.Max(0.25m, points),
            Explanation = string.IsNullOrWhiteSpace(explanation) ? null : explanation.Trim(),
            CreatedBy = createdBy
        };

        byte order = 1;
        foreach (var row in rows)
        {
            question.Answers.Add(new Answer
            {
                Content = row,
                OrderIndex = order,
                IsCorrect = correctSet.Contains(order.ToString()) || correctSet.Contains(row)
            });
            order++;
        }

        if (question.Answers.All(x => !x.IsCorrect))
        {
            error = "Đáp án đúng không khớp với danh sách đáp án.";
            return null;
        }

        if (type == QuestionType.SingleChoice && question.Answers.Count(x => x.IsCorrect) != 1)
        {
            error = "Câu một đáp án chỉ được có đúng một đáp án đúng.";
            return null;
        }

        return question;
    }

    private static bool ValidateExamInput(
        string title,
        GenerateMode generateMode,
        int? questionCount,
        decimal? totalPoints,
        int easyCount,
        int mediumCount,
        int hardCount,
        DateTime startTime,
        DateTime endTime,
        out string error)
    {
        error = "";
        if (string.IsNullOrWhiteSpace(title))
        {
            error = "Kỳ thi cần có tiêu đề.";
            return false;
        }

        if (endTime <= startTime)
        {
            error = "Thời gian kết thúc phải sau thời gian bắt đầu.";
            return false;
        }

        if (generateMode == GenerateMode.ByCount && easyCount + mediumCount + hardCount <= 0 && (!questionCount.HasValue || questionCount.Value <= 0))
        {
            error = "Chế độ sinh theo số câu cần nhập số câu hoặc phân bổ dễ/trung bình/khó.";
            return false;
        }

        if (generateMode == GenerateMode.ByPoints && (!totalPoints.HasValue || totalPoints.Value <= 0))
        {
            error = "Chế độ sinh theo tổng điểm cần nhập tổng điểm lớn hơn 0.";
            return false;
        }

        return true;
    }

    private static async Task<List<List<string>>> ReadCsvRowsAsync(IFormFile file)
    {
        await using var stream = file.OpenReadStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var rows = new List<List<string>>();
        while (await reader.ReadLineAsync() is { } line)
        {
            if (!string.IsNullOrWhiteSpace(line)) rows.Add(ParseCsvLine(line));
        }
        return rows;
    }

    private static async Task<List<List<string>>> ReadXlsxRowsAsync(IFormFile file)
    {
        await using var input = file.OpenReadStream();
        using var archive = new ZipArchive(input, ZipArchiveMode.Read);
        var sharedStrings = ReadSharedStrings(archive);
        var sheet = archive.GetEntry("xl/worksheets/sheet1.xml");
        if (sheet is null) return [];

        await using var sheetStream = sheet.Open();
        var document = await XDocument.LoadAsync(sheetStream, LoadOptions.None, CancellationToken.None);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var rows = new List<List<string>>();
        foreach (var row in document.Descendants(ns + "row"))
        {
            var values = new SortedDictionary<int, string>();
            foreach (var cell in row.Elements(ns + "c"))
            {
                var reference = cell.Attribute("r")?.Value ?? "";
                var columnIndex = ColumnIndex(reference);
                var value = cell.Element(ns + "v")?.Value ?? "";
                if (cell.Attribute("t")?.Value == "s" && int.TryParse(value, out var sharedIndex) && sharedIndex < sharedStrings.Count)
                {
                    value = sharedStrings[sharedIndex];
                }
                else if (cell.Attribute("t")?.Value == "inlineStr")
                {
                    value = cell.Descendants(ns + "t").FirstOrDefault()?.Value ?? "";
                }
                values[columnIndex] = value;
            }
            if (values.Count > 0) rows.Add(values.OrderBy(x => x.Key).Select(x => x.Value).ToList());
        }

        return rows;
    }

    private static List<string> ReadSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null) return [];
        using var stream = entry.Open();
        var document = XDocument.Load(stream);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        return document.Descendants(ns + "si")
            .Select(x => string.Concat(x.Descendants(ns + "t").Select(t => t.Value)))
            .ToList();
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"' && quoted && i + 1 < line.Length && line[i + 1] == '"')
            {
                current.Append('"');
                i++;
            }
            else if (c == '"')
            {
                quoted = !quoted;
            }
            else if (c == ',' && !quoted)
            {
                result.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        result.Add(current.ToString().Trim());
        return result;
    }

    private static bool TryGet(IReadOnlyDictionary<string, string> values, string key, out string value)
    {
        value = "";
        return values.TryGetValue(NormalizeHeader(key), out value!);
    }

    private static string NormalizeHeader(string value)
    {
        return value.Trim().Replace(" ", "", StringComparison.OrdinalIgnoreCase).Replace("_", "", StringComparison.OrdinalIgnoreCase).ToLowerInvariant();
    }

    private static int ColumnIndex(string reference)
    {
        var index = 0;
        foreach (var c in reference.TakeWhile(char.IsLetter))
        {
            index = index * 26 + char.ToUpperInvariant(c) - 'A' + 1;
        }
        return index;
    }

}
