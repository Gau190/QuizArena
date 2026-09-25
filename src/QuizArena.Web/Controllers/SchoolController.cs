using System.Net;
using System.Security.Claims;
using QuizArena.Core.Entities;
using QuizArena.Core.Enums;
using QuizArena.Core.Interfaces;
using QuizArena.Infrastructure.Data;
using QuizArena.Web.Models;
using QuizArena.Web.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace QuizArena.Web.Controllers;

[Authorize]
public class SchoolController(QuizArenaDbContext db, IAuthService authService) : AppController
{
    [HttpGet("/lich-thi")]
    public async Task<IActionResult> Calendar(int? year, int? month, int? classId, int? subjectId, string? view)
    {
        var today = DateTime.Today;
        var selectedYear = year.GetValueOrDefault(today.Year);
        var selectedMonth = month.GetValueOrDefault(today.Month);
        if (selectedMonth is < 1 or > 12)
        {
            selectedYear = today.Year;
            selectedMonth = today.Month;
        }

        var viewMode = string.Equals(view, "week", StringComparison.OrdinalIgnoreCase) ? "week"
            : string.Equals(view, "list", StringComparison.OrdinalIgnoreCase) ? "list"
            : "month";

        var monthStart = new DateTime(selectedYear, selectedMonth, 1);
        var monthEnd = monthStart.AddMonths(1);
        var startUtc = monthStart.ToUniversalTime();
        var endUtc = monthEnd.ToUniversalTime();
        var leadingBlankDays = ((int)monthStart.DayOfWeek + 6) % 7;
        var daysInMonth = DateTime.DaysInMonth(selectedYear, selectedMonth);

        var isStudent = User.IsInRole(nameof(UserRole.Student));
        List<int>? studentClassIds = null;
        if (isStudent)
        {
            studentClassIds = await db.ClassStudents.AsNoTracking()
                .Where(x => x.StudentId == CurrentUserId && x.IsActive)
                .Select(x => x.ClassId)
                .ToListAsync();
        }

        var query = db.Exams.AsNoTracking()
            .Include(x => x.Subject)
            .Include(x => x.Classes).ThenInclude(x => x.Class)
            .Where(x => x.StartTime >= startUtc && x.StartTime < endUtc);
        if (subjectId.HasValue) query = query.Where(x => x.SubjectId == subjectId.Value);
        if (isStudent && studentClassIds is { Count: > 0 })
        {
            var studentExamIds = await db.ExamClasses.AsNoTracking()
                .Where(x => studentClassIds.Contains(x.ClassId))
                .Select(x => x.ExamId)
                .Distinct()
                .ToListAsync();
            query = query.Where(x => studentExamIds.Contains(x.Id));
        }
        else if (isStudent)
        {
            query = query.Where(x => false);
        }
        if (classId.HasValue)
        {
            var assignedExamIds = await db.ExamClasses.AsNoTracking()
                .Where(x => x.ClassId == classId.Value)
                .Select(x => x.ExamId)
                .Distinct()
                .ToListAsync();
            var roomExamIds = await db.ExamRooms.AsNoTracking()
                .Where(x => x.ClassId == classId.Value && x.ExamId.HasValue)
                .Select(x => x.ExamId!.Value)
                .Distinct()
                .ToListAsync();
            var examIds = assignedExamIds.Concat(roomExamIds).Distinct().ToList();
            query = query.Where(x => examIds.Contains(x.Id));
        }

        var exams = await query.OrderBy(x => x.StartTime).ToListAsync();

        var events = exams.Select((exam, index) => ToCalendarEvent(exam, index)).ToList();

        var anchorDay = today.Year == selectedYear && today.Month == selectedMonth
            ? today.Day
            : Math.Min(15, daysInMonth);
        var weekAnchor = new DateTime(selectedYear, selectedMonth, anchorDay);
        var weekStart = weekAnchor.AddDays(-((int)weekAnchor.DayOfWeek + 6) % 7);
        var weekEvents = new List<CalendarEvent>();
        for (var offset = 0; offset < 7; offset++)
        {
            var day = weekStart.AddDays(offset);
            if (day.Month != selectedMonth) continue;
            foreach (var ev in events.Where(x => x.Day == day.Day))
            {
                weekEvents.Add(ev with { StartLocal = day });
            }
        }

        var listRows = exams.Select((exam, index) =>
        {
            var classNames = string.Join(", ", exam.Classes.Select(x => x.Class?.Name).Where(x => !string.IsNullOrWhiteSpace(x)));
            var start = exam.StartTime.ToLocalTime();
            var end = exam.EndTime.ToLocalTime();
            return new CalendarListRow(
                WebUtility.HtmlDecode(exam.Title),
                WebUtility.HtmlDecode(exam.Subject?.Name ?? "Môn thi"),
                string.IsNullOrWhiteSpace(classNames) ? "Chưa gán lớp" : classNames,
                $"{start:dd/MM/yyyy HH:mm} - {end:HH:mm}",
                exam.Subject?.ColorHex ?? Palette(index),
                exam.EndTime < DateTime.UtcNow);
        }).ToList();

        var classes = await db.Classes.AsNoTracking().OrderBy(x => x.Name).ToListAsync();
        var subjects = await db.Subjects.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync();
        var legend = subjects
            .Select((subject, index) => new CalendarLegendItem(WebUtility.HtmlDecode(subject.Name), subject.ColorHex ?? Palette(index)))
            .ToList();

        ViewBag.Classes = isStudent
            ? classes.Where(x => studentClassIds?.Contains(x.Id) == true).ToList()
            : classes;
        ViewBag.Subjects = subjects;
        ViewBag.ClassId = classId;
        ViewBag.SubjectId = subjectId;
        ViewBag.ViewMode = viewMode;
        ViewBag.CanCreateExam = User.IsInRole(nameof(UserRole.Admin)) || User.IsInRole(nameof(UserRole.Teacher));

        return View(new ExamCalendarViewModel(
            $"Tháng {selectedMonth}, {selectedYear}",
            events,
            exams.Where(x => x.EndTime >= DateTime.UtcNow).Take(8).ToList(),
            selectedYear,
            selectedMonth,
            daysInMonth,
            leadingBlankDays,
            legend,
            viewMode,
            weekStart.Day,
            7,
            weekEvents,
            listRows));
    }

    [HttpGet("/ho-so/{id:guid?}")]
    public async Task<IActionResult> StudentProfile(Guid? id, string? tab)
    {
        var model = await BuildStudentProfileAsync(id);
        if (model is not null && (User.IsInRole(nameof(UserRole.Admin)) || User.IsInRole(nameof(UserRole.Teacher))))
        {
            ViewBag.Classes = await db.Classes.AsNoTracking().OrderBy(x => x.Name).ToListAsync();
        }

        ViewBag.Tab = tab switch
        {
            "history" => "history",
            "settings" => "settings",
            _ => "overview"
        };
        return model is null ? NotFound() : View(model);
    }

    [HttpPost("/ho-so/{id:guid}")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> UpdateStudentProfile(
        Guid id,
        string fullName,
        string email,
        string studentCode,
        string? gender,
        DateTime? dateOfBirth,
        string? parentName,
        string? parentPhone,
        string? parentEmail,
        string? address,
        string conduct,
        string? academicLevel,
        int? classId)
    {
        var student = await db.Users.FirstOrDefaultAsync(x => x.Id == id && x.Role == UserRole.Student);
        if (student is null) return NotFound();

        if (!string.IsNullOrWhiteSpace(email) && await db.Users.AnyAsync(x => x.Id != id && x.Email == email.Trim()))
        {
            TempData["SchoolMessage"] = "Email đã được dùng bởi tài khoản khác.";
            return RedirectToAction(nameof(StudentProfile), new { id });
        }

        if (!string.IsNullOrWhiteSpace(studentCode) && await db.StudentProfiles.AnyAsync(x => x.UserId != id && x.StudentCode == studentCode.Trim()))
        {
            TempData["SchoolMessage"] = "Mã thí sinh đã tồn tại.";
            return RedirectToAction(nameof(StudentProfile), new { id });
        }

        student.FullName = string.IsNullOrWhiteSpace(fullName) ? student.FullName : fullName.Trim();
        student.Email = string.IsNullOrWhiteSpace(email) ? student.Email : email.Trim();

        var profile = await db.StudentProfiles.FirstOrDefaultAsync(x => x.UserId == id);
        if (profile is null)
        {
            profile = new StudentProfile { UserId = id, StudentCode = studentCode.Trim() };
            db.StudentProfiles.Add(profile);
        }

        profile.StudentCode = string.IsNullOrWhiteSpace(studentCode) ? profile.StudentCode : studentCode.Trim();
        profile.Gender = gender;
        profile.DateOfBirth = dateOfBirth.HasValue ? DateOnly.FromDateTime(dateOfBirth.Value) : null;
        profile.ParentName = parentName?.Trim();
        profile.ParentPhone = parentPhone?.Trim();
        profile.ParentEmail = parentEmail?.Trim();
        profile.Address = address?.Trim();
        profile.Conduct = string.IsNullOrWhiteSpace(conduct) ? "Tốt" : conduct.Trim();
        profile.AcademicLevel = academicLevel?.Trim();
        profile.UpdatedAt = DateTime.UtcNow;

        if (classId.HasValue && await db.Classes.AnyAsync(x => x.Id == classId.Value))
        {
            var currentAssignments = await db.ClassStudents.Where(x => x.StudentId == id).ToListAsync();
            db.ClassStudents.RemoveRange(currentAssignments);
            db.ClassStudents.Add(new ClassStudent
            {
                StudentId = id,
                ClassId = classId.Value,
                EnrolledAt = DateTime.UtcNow,
                IsActive = true
            });
        }

        await db.SaveChangesAsync();
        TempData["SchoolMessage"] = "Đã cập nhật hồ sơ thí sinh.";
        return RedirectToAction(nameof(StudentProfile), new { id });
    }

    [HttpPost("/ho-so/cai-dat")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> UpdateStudentSettings(string email, string? currentPassword, string? newPassword, string? confirmPassword)
    {
        var student = await db.Users.FirstOrDefaultAsync(x => x.Id == CurrentUserId && x.Role == UserRole.Student);
        if (student is null) return NotFound();

        if (!string.IsNullOrWhiteSpace(email))
        {
            var trimmed = email.Trim();
            if (await db.Users.AnyAsync(x => x.Id != CurrentUserId && x.Email == trimmed))
            {
                TempData["SchoolMessage"] = "Email đã được dùng bởi tài khoản khác.";
                return RedirectToAction(nameof(StudentProfile), new { tab = "settings" });
            }

            student.Email = trimmed;
        }

        if (!string.IsNullOrWhiteSpace(newPassword))
        {
            if (newPassword != confirmPassword)
            {
                TempData["SchoolMessage"] = "Mật khẩu mới và xác nhận không khớp.";
                return RedirectToAction(nameof(StudentProfile), new { tab = "settings" });
            }

            var (success, error) = await authService.ChangePasswordAsync(CurrentUserId, currentPassword ?? "", newPassword);
            if (!success)
            {
                TempData["SchoolMessage"] = error;
                return RedirectToAction(nameof(StudentProfile), new { tab = "settings" });
            }
        }

        await db.SaveChangesAsync();
        TempData["SchoolMessage"] = "Đã cập nhật cài đặt tài khoản.";
        return RedirectToAction(nameof(StudentProfile), new { tab = "settings" });
    }

    [HttpGet("/hoc-ba/{id:guid?}")]
    [Authorize(Roles = "Admin,Teacher,Student")]
    public async Task<IActionResult> Transcript(Guid? id, string? term)
    {
        var profile = await BuildStudentProfileAsync(id);
        if (profile is null) return NotFound();

        var normalizedTerm = NormalizeReportTerm(term);
        var rows = await BuildTranscriptRowsAsync(profile.Student.Id, profile.SubjectScores);
        var rankLabel = profile.ClassRank > 0 && profile.ClassStudentCount > 0
            ? $"#{profile.ClassRank}/{profile.ClassStudentCount}"
            : "—";

        return View(new TranscriptViewModel(profile, rows, normalizedTerm, rankLabel));
    }

    [HttpGet("/hoc-ba/{id:guid?}/xuat")]
    [Authorize(Roles = "Admin,Teacher,Student")]
    public async Task<IActionResult> ExportTranscript(Guid? id)
    {
        var profile = await BuildStudentProfileAsync(id);
        if (profile is null) return NotFound();

        var rows = profile.SubjectScores.Select(x => new[]
        {
            profile.Student.FullName,
            profile.ClassName,
            x.Subject,
            x.AverageScore.ToString("0.0"),
            x.Attempts.ToString(),
            x.LastAttempt
        }).ToList();
        if (profile.SubjectScores.Count == 0)
        {
            rows.Add([profile.Student.FullName, profile.ClassName, "Chưa có dữ liệu", "0.0", "0", ""]);
        }
        var bytes = SpreadsheetExporter.CreateXlsx(
            "Hoc ba",
            ["Student", "Class", "Subject", "AverageScore", "Attempts", "LastAttempt"],
            rows);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"hoc-ba-{profile.Student.Username}.xlsx");
    }

    [HttpGet("/thong-bao")]
    public async Task<IActionResult> Notifications(string? box, string? category)
    {
        var role = User.FindFirstValue(ClaimTypes.Role);
        var query = NotificationsForRole(db.Notifications.AsNoTracking(), role);

        if (string.Equals(box, "unread", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => !x.IsRead);
        }
        if (string.Equals(box, "sent", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.CreatedBy == CurrentUserId);
        }
        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(x => x.Category == category);
        }

        var notifications = await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(50)
            .ToListAsync();
        var items = notifications.Select(x => new NotificationItem(
            x.Title,
            x.Body,
            x.Category,
            RelativeTime(x.CreatedAt),
            !x.IsRead,
            NotificationTone(x.Category))).ToList();
        var inbox = NotificationsForRole(db.Notifications.AsNoTracking(), role);
        var unreadCount = await inbox.CountAsync(x => !x.IsRead);

        ViewBag.Box = box;
        ViewBag.Category = category;
        return View(new NotificationsViewModel(unreadCount, items));
    }

    [HttpPost("/thong-bao/da-doc")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkNotificationsRead()
    {
        var role = User.FindFirstValue(ClaimTypes.Role);
        var unread = await NotificationsForRole(db.Notifications.Where(x => !x.IsRead), role).ToListAsync();
        foreach (var item in unread)
        {
            item.IsRead = true;
        }
        await db.SaveChangesAsync();
        TempData["SchoolMessage"] = "Đã đánh dấu tất cả thông báo là đã đọc.";
        return RedirectToAction(nameof(Notifications));
    }

    [HttpPost("/thong-bao/soan-tin")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> ComposeNotification(string title, string body, string category)
    {
        db.Notifications.Add(new Notification
        {
            Title = string.IsNullOrWhiteSpace(title) ? "Thông báo mới" : title.Trim(),
            Body = string.IsNullOrWhiteSpace(body) ? "Nội dung đang được cập nhật." : body.Trim(),
            Category = string.IsNullOrWhiteSpace(category) ? "Hệ thống" : category.Trim(),
            CreatedBy = CurrentUserId
        });
        await db.SaveChangesAsync();
        TempData["SchoolMessage"] = $"Đã gửi thông báo nhóm {category}.";
        return RedirectToAction(nameof(Notifications));
    }

    [HttpGet("/bao-cao")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> Reports(string? term)
    {
        var allAttempts = await db.ExamAttempts.AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.Exam).ThenInclude(x => x!.Subject)
            .Where(x => x.Score.HasValue)
            .ToListAsync();
        var attempts = FilterAttemptsByTerm(allAttempts, term);
        ViewBag.Term = NormalizeReportTerm(term);
        var exams = await db.Exams.AsNoTracking().CountAsync();
        var avg = attempts.Count == 0 ? 0 : decimal.Round(attempts.Average(x => x.Score ?? 0), 1);
        var passRate = attempts.Count == 0 ? 0 : attempts.Count(x => (x.Score ?? 0) >= 5) * 100 / attempts.Count;

        var subjectMetrics = attempts
            .Where(x => x.Exam?.Subject is not null)
            .GroupBy(x => new { x.Exam!.SubjectId, x.Exam.Subject!.Name, x.Exam.Subject.ColorHex })
            .Select((group, index) => new SubjectMetric(
                WebUtility.HtmlDecode(group.Key.Name),
                decimal.Round(group.Average(x => x.Score ?? 0), 1),
                group.Count(),
                group.Key.ColorHex ?? Palette(index)))
            .DefaultIfEmpty(new SubjectMetric("Chưa có dữ liệu", 0, 0, "#3B82F6"))
            .ToList();

        var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-5);
        var monthlyScores = Enumerable.Range(0, 6)
            .Select(offset =>
            {
                var start = monthStart.AddMonths(offset);
                var end = start.AddMonths(1);
                var rows = attempts.Where(x =>
                    x.SubmittedAt.HasValue &&
                    x.SubmittedAt.Value >= start.ToUniversalTime() &&
                    x.SubmittedAt.Value < end.ToUniversalTime()).ToList();
                var average = rows.Count == 0 ? 0 : decimal.Round(rows.Average(x => x.Score ?? 0), 1);
                return new MonthlyScorePoint($"Th.{start.Month}", average, rows.Count);
            })
            .ToList();

        var classAssignments = await db.ClassStudents.AsNoTracking()
            .Include(x => x.Class)
            .Where(x => x.IsActive)
            .ToListAsync();
        var classByStudent = classAssignments
            .GroupBy(x => x.StudentId)
            .ToDictionary(
                x => x.Key,
                x => x.OrderByDescending(row => row.EnrolledAt).First().Class?.Name ?? "Chưa gán lớp");

        var distributionDefinitions = new (string Label, Func<decimal, bool> Match, string Color)[]
        {
            ("Xuất sắc (9-10)", score => score >= 9, "#2563EB"),
            ("Giỏi (8-8.9)", score => score >= 8 && score < 9, "#16A34A"),
            ("Khá (6.5-7.9)", score => score >= 6.5m && score < 8, "#C2410C"),
            ("Trung bình (5-6.4)", score => score >= 5 && score < 6.5m, "#9CA3AF"),
            ("Yếu (<5)", score => score < 5, "#DC2626")
        };
        var scoreDistribution = distributionDefinitions
            .Select(item =>
            {
                var count = attempts.Count(x => item.Match(x.Score ?? 0));
                var percent = attempts.Count == 0 ? 0 : count * 100 / attempts.Count;
                return new ScoreDistributionPoint(item.Label, count, percent, item.Color);
            })
            .ToList();

        var classMetrics = attempts
            .Select(x => new
            {
                Attempt = x,
                ClassName = classByStudent.TryGetValue(x.UserId, out var className) ? className : "Chưa gán lớp"
            })
            .GroupBy(x => x.ClassName)
            .Select((group, index) =>
            {
                var rows = group.Select(x => x.Attempt).ToList();
                var classPassRate = rows.Count == 0 ? 0 : rows.Count(x => (x.Score ?? 0) >= 5) * 100 / rows.Count;
                return new ClassScoreMetric(
                    group.Key,
                    decimal.Round(rows.Average(x => x.Score ?? 0), 1),
                    rows.Count,
                    classPassRate,
                    Palette(index));
            })
            .OrderByDescending(x => x.AverageScore)
            .Take(8)
            .ToList();

        var topStudents = attempts
            .Where(x => x.User is not null)
            .GroupBy(x => new { x.UserId, x.User!.FullName })
            .Select(group =>
            {
                var rows = group.ToList();
                var studentPassRate = rows.Count == 0 ? 0 : rows.Count(x => (x.Score ?? 0) >= 5) * 100 / rows.Count;
                return new
                {
                    group.Key.UserId,
                    StudentName = group.Key.FullName,
                    AverageScore = decimal.Round(rows.Average(x => x.Score ?? 0), 1),
                    Attempts = rows.Count,
                    PassRate = studentPassRate
                };
            })
            .OrderByDescending(x => x.AverageScore)
            .ThenByDescending(x => x.Attempts)
            .Take(10)
            .Select((x, index) => new TopStudentReportRow(
                index + 1,
                x.StudentName,
                classByStudent.TryGetValue(x.UserId, out var className) ? className : "Chưa gán lớp",
                x.AverageScore,
                x.Attempts,
                x.PassRate))
            .ToList();

        var model = new AnalyticsReportViewModel(
            [
                new("Tổng kỳ thi", exams.ToString(), "đã tạo trong hệ thống", "primary"),
                new("Lượt dự thi", attempts.Count.ToString(), "có điểm sau khi nộp bài", "success"),
                new("Điểm trung bình", avg.ToString("0.0"), "/ 10.0", "primary"),
                new("Tỷ lệ đạt", $"{passRate}%", "điểm từ 5 trở lên", "warning")
            ],
            subjectMetrics,
            [
                new("fa-chart-line", "Điểm trung bình theo môn được tổng hợp trực tiếp từ bài đã nộp.", "tự động", "primary"),
                new("fa-circle-exclamation", "Các môn có điểm dưới 6.5 nên được ưu tiên ôn tập và rà câu hỏi.", "gợi ý", "warning"),
                new("fa-file-export", "Có thể dùng màn này làm nền cho xuất Excel/PDF ở giai đoạn báo cáo tiếp theo.", "kế hoạch", "success")
            ],
            monthlyScores,
            scoreDistribution,
            classMetrics,
            topStudents);

        return View(model);
    }

    [HttpGet("/bao-cao/xuat")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> ExportReports(string? term)
    {
        var allAttempts = await db.ExamAttempts.AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.Exam).ThenInclude(x => x!.Subject)
            .OrderByDescending(x => x.StartedAt)
            .ToListAsync();
        var attempts = FilterAttemptsByTerm(allAttempts.Where(x => x.Score.HasValue).ToList(), term);
        var rows = attempts.Select(x => new[]
        {
            x.User?.FullName,
            x.Exam?.Title,
            WebUtility.HtmlDecode(x.Exam?.Subject?.Name ?? ""),
            x.Status.ToString(),
            x.Score?.ToString("0.##"),
            x.SubmittedAt?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? ""
        }.Select(value => value ?? "").ToArray());
        var bytes = SpreadsheetExporter.CreateXlsx(
            "Bao cao",
            ["Student", "Exam", "Subject", "Status", "Score", "SubmittedAt"],
            rows);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "quizarena-report.xlsx");
    }

    [HttpGet("/giang-vien/cau-truc-de")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> ExamBlueprint(int? subjectId)
    {
        var subjects = await db.Subjects.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .ToListAsync();
        if (subjects.Count == 0) return View(new ExamBlueprintViewModel([], [], 0, 0, 0, GenerateMode.ByCount, 10));

        var selectedSubjectId = subjectId ?? subjects[0].Id;
        if (!subjects.Any(x => x.Id == selectedSubjectId)) selectedSubjectId = subjects[0].Id;

        var items = await db.ExamBlueprintItems.AsNoTracking()
            .Where(x => x.CreatedBy == CurrentUserId && x.SubjectId == selectedSubjectId)
            .OrderBy(x => x.Id)
            .ToListAsync();
        var rows = items.Count == 0
            ? new List<ExamBlueprintRow>
            {
                new("Chương 1 - Nhận biết nền tảng", 4, 2, 0, 3.0m),
                new("Chương 2 - Vận dụng", 2, 4, 1, 4.0m),
                new("Chương 3 - Tổng hợp", 1, 2, 2, 3.0m)
            }
            : items.Select(x => new ExamBlueprintRow(x.Chapter, x.EasyCount, x.MediumCount, x.HardCount, x.Points)).ToList();

        var config = await db.ExamBlueprintConfigs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == CurrentUserId && x.SubjectId == selectedSubjectId);
        var generateMode = config?.GenerateMode ?? GenerateMode.ByCount;
        var targetPoints = config?.TargetTotalPoints ?? rows.Sum(x => x.Points);

        return View(new ExamBlueprintViewModel(
            subjects,
            rows,
            rows.Sum(x => x.EasyCount + x.MediumCount + x.HardCount),
            rows.Sum(x => x.Points),
            selectedSubjectId,
            generateMode,
            targetPoints));
    }

    [HttpPost("/giang-vien/cau-truc-de")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> SaveExamBlueprint(
        int subjectId,
        GenerateMode generateMode,
        decimal targetTotalPoints,
        string[] chapter,
        int[] easyCount,
        int[] mediumCount,
        int[] hardCount,
        decimal[] points)
    {
        if (!await db.Subjects.AnyAsync(x => x.Id == subjectId && x.IsActive))
        {
            TempData["SchoolMessage"] = "Môn thi không hợp lệ.";
            return RedirectToAction(nameof(ExamBlueprint));
        }

        var existing = await db.ExamBlueprintItems
            .Where(x => x.CreatedBy == CurrentUserId && x.SubjectId == subjectId)
            .ToListAsync();
        db.ExamBlueprintItems.RemoveRange(existing);

        for (var i = 0; i < chapter.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(chapter[i])) continue;
            db.ExamBlueprintItems.Add(new ExamBlueprintItem
            {
                SubjectId = subjectId,
                Chapter = chapter[i].Trim(),
                EasyCount = i < easyCount.Length ? Math.Max(0, easyCount[i]) : 0,
                MediumCount = i < mediumCount.Length ? Math.Max(0, mediumCount[i]) : 0,
                HardCount = i < hardCount.Length ? Math.Max(0, hardCount[i]) : 0,
                Points = i < points.Length ? Math.Max(0, points[i]) : 0,
                CreatedBy = CurrentUserId
            });
        }

        var config = await db.ExamBlueprintConfigs
            .FirstOrDefaultAsync(x => x.UserId == CurrentUserId && x.SubjectId == subjectId);
        if (config is null)
        {
            config = new ExamBlueprintConfig { UserId = CurrentUserId, SubjectId = subjectId };
            db.ExamBlueprintConfigs.Add(config);
        }

        config.GenerateMode = generateMode;
        config.TargetTotalPoints = Math.Max(0, targetTotalPoints);
        config.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        TempData["SchoolMessage"] = "Đã lưu cấu trúc đề vào cơ sở dữ liệu.";
        return RedirectToAction(nameof(ExamBlueprint), new { subjectId });
    }

    [HttpGet("/giang-vien/phan-hoi-cau-hoi")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> QuestionReports()
    {
        var savedReports = await db.QuestionReports.AsNoTracking()
            .Include(x => x.Question).ThenInclude(x => x!.Subject)
            .Include(x => x.Reporter)
            .OrderByDescending(x => x.CreatedAt)
            .Take(50)
            .ToListAsync();
        var reports = savedReports.Select(report => new QuestionReportRow(
            report.QuestionId,
            WebUtility.HtmlDecode(report.Question?.Subject?.Name ?? "Môn thi"),
            report.Question?.Content ?? "Câu hỏi đã bị xóa",
            report.Reason,
            report.Reporter?.FullName ?? "Thí sinh",
            report.Status == "Resolved" ? "Đã xử lý" : "Chờ duyệt",
            report.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy"))).ToList();

        if (reports.Count == 0)
        {
            reports.Add(new QuestionReportRow(0, "Chưa có dữ liệu", "Chưa có câu hỏi được báo cáo.", "Không có", "Hệ thống", "Trống", DateTime.Now.ToString("dd/MM/yyyy")));
        }

        return View(new QuestionReportsViewModel(reports));
    }

    [HttpPost("/giang-vien/phan-hoi-cau-hoi/{questionId:int}/xu-ly")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> ReviewQuestionReport(int questionId)
    {
        var report = await db.QuestionReports
            .Where(x => x.QuestionId == questionId && x.Status != "Resolved")
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync();
        if (report is null)
        {
            TempData["SchoolMessage"] = "Không có phản hồi nào cần xử lý.";
            return RedirectToAction(nameof(QuestionReports));
        }

        report.Status = "Resolved";
        report.ReviewedAt = DateTime.UtcNow;
        report.TeacherNote = $"Đã xem xét bởi {User.Identity?.Name ?? "Admin"}";
        await db.SaveChangesAsync();
        TempData["SchoolMessage"] = $"Đã đánh dấu Q{questionId} là đã xử lý.";
        return RedirectToAction(nameof(QuestionReports));
    }

    private async Task<StudentProfileViewModel?> BuildStudentProfileAsync(Guid? requestedId)
    {
        var role = User.FindFirstValue(ClaimTypes.Role);
        var id = role == nameof(UserRole.Student) ? CurrentUserId : requestedId;
        var student = id.HasValue
            ? await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id.Value && x.Role == UserRole.Student)
            : await db.Users.AsNoTracking().Where(x => x.Role == UserRole.Student).OrderBy(x => x.FullName).FirstOrDefaultAsync();

        if (student is null) return null;

        var attempts = await db.ExamAttempts.AsNoTracking()
            .Include(x => x.Exam).ThenInclude(x => x!.Subject)
            .Where(x => x.UserId == student.Id)
            .OrderByDescending(x => x.StartedAt)
            .ToListAsync();
        var completed = attempts.Where(x => x.Score.HasValue).ToList();
        var profile = await db.StudentProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == student.Id);
        var assignment = await db.ClassStudents.AsNoTracking()
            .Include(x => x.Class)
            .Where(x => x.StudentId == student.Id)
            .OrderByDescending(x => x.EnrolledAt)
            .FirstOrDefaultAsync();
        var average = completed.Count == 0 ? 0 : decimal.Round(completed.Average(x => x.Score ?? 0), 1);
        var passRate = completed.Count == 0 ? 0 : completed.Count(x => (x.Score ?? 0) >= 5) * 100 / completed.Count;
        var subjectScores = completed
            .Where(x => x.Exam?.Subject is not null)
            .GroupBy(x => x.Exam!.Subject!.Name)
            .Select((group, index) => new StudentScoreRow(
                WebUtility.HtmlDecode(group.Key),
                decimal.Round(group.Average(x => x.Score ?? 0), 1),
                group.Count(),
                group.Max(x => x.SubmittedAt ?? x.StartedAt).ToLocalTime().ToString("dd/MM/yyyy"),
                Palette(index)))
            .ToList();

        var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-5);
        var monthlyScores = Enumerable.Range(0, 6)
            .Select(offset =>
            {
                var start = monthStart.AddMonths(offset);
                var end = start.AddMonths(1);
                var rows = completed.Where(x =>
                    x.SubmittedAt.HasValue &&
                    x.SubmittedAt.Value.ToLocalTime() >= start &&
                    x.SubmittedAt.Value.ToLocalTime() < end).ToList();
                var monthAverage = rows.Count == 0 ? 0 : decimal.Round(rows.Average(x => x.Score ?? 0), 1);
                return new StudentMonthlyPoint($"Th.{start.Month}", monthAverage);
            })
            .ToList();

        var classRank = 0;
        var classStudentCount = 0;
        var classAverage = 0m;
        if (assignment?.ClassId is int classId)
        {
            var classStudentIds = await db.ClassStudents.AsNoTracking()
                .Where(x => x.ClassId == classId && x.IsActive)
                .Select(x => x.StudentId)
                .ToListAsync();
            classStudentCount = classStudentIds.Count;
            if (classStudentCount > 0)
            {
                var classAttempts = await db.ExamAttempts.AsNoTracking()
                    .Where(x => classStudentIds.Contains(x.UserId) && x.Score.HasValue)
                    .ToListAsync();
                var averages = classStudentIds
                    .Select(studentId =>
                    {
                        var rows = classAttempts.Where(x => x.UserId == studentId).ToList();
                        return rows.Count == 0 ? 0m : decimal.Round(rows.Average(x => x.Score ?? 0), 1);
                    })
                    .OrderByDescending(x => x)
                    .ToList();
                classAverage = averages.Count == 0 ? 0 : decimal.Round(averages.Average(), 1);
                if (average > 0)
                {
                    classRank = averages.Count(x => x > average) + 1;
                }
                else
                {
                    classRank = classStudentCount;
                }
            }
        }

        return new StudentProfileViewModel(
            student,
            profile,
            attempts,
            subjectScores,
            average,
            completed.Count,
            passRate,
            assignment?.Class?.Name ?? "Chưa xếp lớp",
            profile?.StudentCode ?? $"TS-{student.Id.ToString()[..4].ToUpperInvariant()}",
            classRank,
            classStudentCount,
            classAverage,
            monthlyScores);
    }

    private static CalendarEvent ToCalendarEvent(Exam exam, int paletteIndex)
    {
        var classNames = string.Join(" ", exam.Classes
            .Select(x => x.Class?.Name)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Take(2));
        var decodedTitle = WebUtility.HtmlDecode(exam.Title);
        var label = string.IsNullOrWhiteSpace(classNames)
            ? TruncateCalendarLabel(decodedTitle, 22)
            : $"{TruncateCalendarLabel(decodedTitle, 14)} {classNames}";
        var start = exam.StartTime.ToLocalTime();
        return new CalendarEvent(
            start.Day,
            label,
            WebUtility.HtmlDecode(exam.Subject?.Name ?? "Môn thi"),
            $"{start:HH:mm} - {exam.EndTime.ToLocalTime():HH:mm}",
            exam.Subject?.ColorHex ?? Palette(paletteIndex),
            exam.EndTime < DateTime.UtcNow,
            start);
    }

    private static string TruncateCalendarLabel(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength) return value;
        return value[..Math.Max(0, maxLength - 1)].TrimEnd() + "…";
    }

    private static string NormalizeReportTerm(string? term) => term switch
    {
        "1" => "1",
        "year" => "year",
        _ => "2"
    };

    private static List<ExamAttempt> FilterAttemptsByTerm(IReadOnlyList<ExamAttempt> attempts, string? term)
    {
        var normalized = NormalizeReportTerm(term);
        if (normalized == "year") return attempts.ToList();

        return attempts.Where(x =>
        {
            var submitted = x.SubmittedAt?.ToLocalTime() ?? x.StartedAt.ToLocalTime();
            var month = submitted.Month;
            return normalized == "1"
                ? month is >= 8 and <= 12
                : month is >= 1 and <= 7;
        }).ToList();
    }

    private static string NotificationTone(string category) => category switch
    {
        "Kết quả" => "success",
        "Cảnh báo" => "warning",
        "Hệ thống" => "purple",
        _ => "primary"
    };

    private static string RelativeTime(DateTime utc)
    {
        var span = DateTime.UtcNow - utc;
        if (span.TotalMinutes < 1) return "vừa xong";
        if (span.TotalHours < 1) return $"{Math.Floor(span.TotalMinutes)} phút trước";
        if (span.TotalDays < 1) return $"{Math.Floor(span.TotalHours)} giờ trước";
        return $"{Math.Floor(span.TotalDays)} ngày trước";
    }

    private static IQueryable<Notification> NotificationsForRole(IQueryable<Notification> query, string? role) =>
        string.IsNullOrWhiteSpace(role)
            ? query
            : query.Where(x => x.RecipientRole == null || x.RecipientRole == role);

    private static string Palette(int index)
    {
        var colors = new[] { "#3B82F6", "#22C55E", "#F97316", "#94A3B8", "#0D9488", "#EF4444" };
        return colors[index % colors.Length];
    }

    private async Task<List<TranscriptSubjectRow>> BuildTranscriptRowsAsync(Guid studentId, IReadOnlyList<StudentScoreRow> fallbackScores)
    {
        var semesters = await db.Semesters.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
        var hk1 = semesters.FirstOrDefault(x => x.Name.Contains('1', StringComparison.OrdinalIgnoreCase));
        var hk2 = semesters.FirstOrDefault(x => x.Name.Contains('2', StringComparison.OrdinalIgnoreCase));

        var records = await db.AcademicRecords.AsNoTracking()
            .Include(x => x.Subject)
            .Where(x => x.StudentId == studentId)
            .ToListAsync();

        if (records.Count > 0)
        {
            var subjectIds = records.Select(x => x.SubjectId).Distinct();
            return subjectIds.Select(subjectId =>
            {
                var subjectName = WebUtility.HtmlDecode(records.First(x => x.SubjectId == subjectId).Subject?.Name ?? "Môn học");
                var s1 = records.FirstOrDefault(x => x.SubjectId == subjectId && hk1 != null && x.SemesterId == hk1.Id);
                var s2 = records.FirstOrDefault(x => x.SubjectId == subjectId && hk2 != null && x.SemesterId == hk2.Id);
                var semester1 = s1?.AverageScore ?? 0;
                var semester2 = s2?.AverageScore ?? 0;
                var finalScore = s1 is not null && s2 is not null
                    ? decimal.Round((semester1 + semester2 * 2) / 3, 1)
                    : s2?.AverageScore ?? s1?.AverageScore ?? 0;
                var attempts = (s1?.TotalAttempts ?? 0) + (s2?.TotalAttempts ?? 0);
                return new TranscriptSubjectRow(subjectName, semester1, semester2, finalScore, Rank(finalScore), attempts);
            }).OrderBy(x => x.Subject).ToList();
        }

        return fallbackScores.Select(score =>
        {
            var semester1 = Clamp(score.AverageScore - 0.3m);
            var semester2 = Clamp(score.AverageScore + 0.2m);
            var finalScore = decimal.Round((semester1 + semester2 * 2) / 3, 1);
            return new TranscriptSubjectRow(score.Subject, semester1, semester2, finalScore, Rank(finalScore), score.Attempts);
        }).ToList();
    }

    private static decimal Clamp(decimal value) => Math.Min(10, Math.Max(0, decimal.Round(value, 1)));

    private static string Rank(decimal score)
    {
        if (score >= 9) return "Xuất sắc";
        if (score >= 8) return "Giỏi";
        if (score >= 6.5m) return "Khá";
        if (score >= 5) return "Trung bình";
        return "Yếu";
    }

    private static string Csv(IEnumerable<string?> values)
    {
        return string.Join(",", values.Select(value =>
        {
            var text = value ?? "";
            return $"\"{text.Replace("\"", "\"\"")}\"";
        }));
    }
}
