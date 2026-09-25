using QuizArena.Core.Entities;
using QuizArena.Web.Utilities;
using QuizArena.Core.Enums;
using QuizArena.Infrastructure.Data;
using QuizArena.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace QuizArena.Web.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController(QuizArenaDbContext db, IMemoryCache cache, IWebHostEnvironment env) : AppController
{
    [HttpGet("/quan-tri")]
    [HttpGet("/quan-tri/tong-quan")]
    public async Task<IActionResult> Dashboard()
    {
        var now = DateTime.UtcNow;
        var users = await db.Users.AsNoTracking().ToListAsync();
        var exams = await db.Exams.AsNoTracking()
            .Include(x => x.Subject)
            .OrderByDescending(x => x.StartTime)
            .ToListAsync();
        var attempts = await db.ExamAttempts.AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.Exam).ThenInclude(x => x!.Subject)
            .OrderByDescending(x => x.StartedAt)
            .ToListAsync();
        var questionCount = await db.Questions.AsNoTracking().CountAsync();

        var subjectMetrics = attempts
            .Where(x => x.Score.HasValue && x.Exam?.Subject is not null)
            .GroupBy(x => x.Exam!.Subject!.Name)
            .Select((group, index) => new SubjectMetric(
                group.Key,
                decimal.Round(group.Average(x => x.Score ?? 0), 1),
                group.Count(),
                Palette(index)))
            .DefaultIfEmpty(new SubjectMetric("Chưa có dữ liệu", 0, 0, "#3B82F6"))
            .ToList();

        var model = new AdminDashboardViewModel(
            [
                new("Tổng thí sinh", users.Count(x => x.Role == UserRole.Student).ToString(), "+ dữ liệu theo tài khoản hiện có", "primary"),
                new("Giáo viên", users.Count(x => x.Role == UserRole.Teacher).ToString(), "quản lý ngân hàng câu hỏi", "success"),
                new("Kỳ thi đang mở", exams.Count(x => x.IsActive && x.StartTime <= now && x.EndTime >= now).ToString(), "đang trong thời gian làm bài", "warning"),
                new("Câu hỏi", questionCount.ToString("N0"), "trong ngân hàng đề", "purple")
            ],
            subjectMetrics,
            BuildActivities(exams, attempts, users),
            exams.Where(x => x.EndTime >= now).OrderBy(x => x.StartTime).Take(6).ToList());

        return View(model);
    }

    [HttpGet("/quan-tri/nguoi-dung")]
    public async Task<IActionResult> Users(string? q, UserRole? role)
    {
        var users = db.Users.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            users = users.Where(x => x.FullName.Contains(q) || x.Username.Contains(q) || x.Email.Contains(q));
        }
        if (role.HasValue) users = users.Where(x => x.Role == role);
        return View(await users.OrderBy(x => x.Role).ThenBy(x => x.FullName).ToListAsync());
    }

    [HttpPost("/quan-tri/nguoi-dung")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser(string username, string email, string fullName, string password, UserRole role)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(fullName))
        {
            TempData["AdminMessage"] = "Vui lòng nhập đầy đủ tên đăng nhập, email và họ tên.";
            return RedirectToAction(nameof(Users));
        }

        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
        {
            TempData["AdminMessage"] = "Mật khẩu cần tối thiểu 6 ký tự.";
            return RedirectToAction(nameof(Users));
        }

        username = username.Trim();
        email = email.Trim();
        if (await db.Users.AnyAsync(x => x.Username == username || x.Email == email))
        {
            TempData["AdminMessage"] = "Username hoặc email đã tồn tại.";
            return RedirectToAction(nameof(Users));
        }

        var created = new User { Username = username, Email = email, FullName = fullName.Trim(), PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, 12), Role = role };
        db.Users.Add(created);
        if (role == UserRole.Student)
        {
            db.StudentProfiles.Add(new StudentProfile { UserId = created.Id, StudentCode = $"TS-{created.Id.ToString()[..4].ToUpperInvariant()}", Conduct = "Tốt" });
        }

        await db.SaveChangesAsync();
        TempData["AdminMessage"] = $"Đã tạo tài khoản {username}.";
        return RedirectToAction(nameof(Users));
    }

    [HttpPost("/quan-tri/nguoi-dung/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateUser(Guid id, string username, string email, string fullName, UserRole role)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound();

        username = username.Trim();
        email = email.Trim();
        if (await db.Users.AnyAsync(x => x.Id != id && (x.Username == username || x.Email == email)))
        {
            TempData["AdminMessage"] = "Username hoặc email đã được dùng bởi tài khoản khác.";
            return RedirectToAction(nameof(Users));
        }

        user.Username = username;
        user.Email = email;
        user.FullName = fullName.Trim();
        user.Role = role;
        await db.SaveChangesAsync();
        TempData["AdminMessage"] = $"Đã cập nhật tài khoản {user.Username}.";
        return RedirectToAction(nameof(Users));
    }

    [HttpPost("/quan-tri/nguoi-dung/{id:guid}/dat-lai-mat-khau")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetUserPassword(Guid id, string password)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound();
        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
        {
            TempData["AdminMessage"] = "Mật khẩu mới cần tối thiểu 6 ký tự.";
            return RedirectToAction(nameof(Users));
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, 12);
        await db.SaveChangesAsync();
        TempData["AdminMessage"] = $"Đã reset mật khẩu cho {user.Username}.";
        return RedirectToAction(nameof(Users));
    }

    [HttpPost("/quan-tri/nguoi-dung/{id:guid}/xoa")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        if (id == CurrentUserId)
        {
            TempData["AdminMessage"] = "Không thể xóa chính tài khoản đang đăng nhập.";
            return RedirectToAction(nameof(Users));
        }

        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound();

        var hasDependencies =
            await db.Subjects.AnyAsync(x => x.CreatedBy == id) ||
            await db.Questions.AnyAsync(x => x.CreatedBy == id) ||
            await db.Exams.AnyAsync(x => x.CreatedBy == id) ||
            await db.ExamAttempts.AnyAsync(x => x.UserId == id) ||
            await db.ExamBlueprintItems.AnyAsync(x => x.CreatedBy == id);
        if (hasDependencies)
        {
            TempData["AdminMessage"] = "Không thể xóa tài khoản đã có dữ liệu kỳ thi, câu hỏi hoặc lượt làm bài. Hãy khóa tài khoản nếu không dùng nữa.";
            return RedirectToAction(nameof(Users));
        }

        var classStudents = await db.ClassStudents.Where(x => x.StudentId == id).ToListAsync();
        db.ClassStudents.RemoveRange(classStudents);
        var activeSession = await db.ActiveSessions.FirstOrDefaultAsync(x => x.UserId == id);
        if (activeSession is not null) db.ActiveSessions.Remove(activeSession);
        var studentProfile = await db.StudentProfiles.FirstOrDefaultAsync(x => x.UserId == id);
        if (studentProfile is not null) db.StudentProfiles.Remove(studentProfile);
        var teacherProfile = await db.TeacherProfiles.FirstOrDefaultAsync(x => x.UserId == id);
        if (teacherProfile is not null) db.TeacherProfiles.Remove(teacherProfile);
        var homerooms = await db.Classes.Where(x => x.HomeTeacherId == id).ToListAsync();
        foreach (var item in homerooms) item.HomeTeacherId = null;
        var examRooms = await db.ExamRooms.Where(x => x.ProctorId == id).ToListAsync();
        foreach (var room in examRooms)
        {
            room.ProctorId = null;
            room.ProctorName = null;
        }

        db.Users.Remove(user);
        await db.SaveChangesAsync();
        TempData["AdminMessage"] = $"Đã xóa tài khoản {user.Username}.";
        return RedirectToAction(nameof(Users));
    }

    [HttpPost("/quan-tri/nguoi-dung/{id:guid}/bat-tat")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleUser(Guid id)
    {
        var user = await db.Users.FindAsync(id);
        if (user is not null && user.Id != CurrentUserId)
        {
            user.IsActive = !user.IsActive;
            await db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Users));
    }

    [HttpGet("/quan-tri/mon-thi")]
    public async Task<IActionResult> Subjects()
    {
        var subjects = await cache.GetOrCreateAsync("subjects_active_admin", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
            return await db.Subjects.AsNoTracking().Include(x => x.Creator).OrderBy(x => x.Name).ToListAsync();
        });
        return View(subjects);
    }

    [HttpPost("/quan-tri/mon-thi")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSubject(string name, string? description)
    {
        db.Subjects.Add(new Subject { Name = name, Description = description, CreatedBy = CurrentUserId });
        await db.SaveChangesAsync();
        cache.Remove("subjects_active_admin");
        return RedirectToAction(nameof(Subjects));
    }

    [HttpPost("/quan-tri/mon-thi/{id:int}/bat-tat")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleSubject(int id)
    {
        var subject = await db.Subjects.FindAsync(id);
        if (subject is not null)
        {
            subject.IsActive = !subject.IsActive;
            await db.SaveChangesAsync();
            cache.Remove("subjects_active_admin");
        }
        return RedirectToAction(nameof(Subjects));
    }

    [HttpPost("/quan-tri/mon-thi/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateSubject(int id, string name, string? description)
    {
        var subject = await db.Subjects.FindAsync(id);
        if (subject is null) return NotFound();

        name = name.Trim();
        if (await db.Subjects.AnyAsync(x => x.Id != id && x.Name == name))
        {
            TempData["AdminMessage"] = "Tên môn thi đã tồn tại.";
            return RedirectToAction(nameof(Subjects));
        }

        subject.Name = name;
        subject.Description = description?.Trim();
        await db.SaveChangesAsync();
        cache.Remove("subjects_active_admin");
        TempData["AdminMessage"] = $"Đã cập nhật môn {subject.Name}.";
        return RedirectToAction(nameof(Subjects));
    }

    [HttpGet("/quan-tri/lop-hoc")]
    public async Task<IActionResult> Classes(string? q)
    {
        var organization = await GetOrCreateOrganizationAsync();
        var academicYear = await GetOrCreateCurrentAcademicYearAsync(organization.Id);
        var teachers = await db.Users.AsNoTracking()
            .Where(x => x.Role == UserRole.Teacher)
            .OrderBy(x => x.FullName)
            .ToListAsync();
        var academicYears = await db.AcademicYears.AsNoTracking()
            .Where(x => x.OrganizationId == organization.Id)
            .OrderByDescending(x => x.Name)
            .ToListAsync();
        var attempts = await db.ExamAttempts.AsNoTracking().Where(x => x.Score.HasValue).ToListAsync();
        var avg = attempts.Count == 0 ? 0 : decimal.Round(attempts.Average(x => x.Score ?? 0), 1);

        if (!await db.Classes.AnyAsync(x => x.AcademicYearId == academicYear.Id))
        {
            db.Classes.Add(new SchoolClass
            {
                OrganizationId = organization.Id,
                AcademicYearId = academicYear.Id,
                Name = "12A1",
                Grade = "12",
                Track = "Khoa học tự nhiên",
                Room = "Phòng 301",
                HomeTeacherId = teachers.FirstOrDefault()?.Id
            });
            await db.SaveChangesAsync();
        }

        var classEntities = await db.Classes.AsNoTracking()
            .Include(x => x.HomeTeacher)
            .Include(x => x.Students)
            .Where(x => x.AcademicYearId == academicYear.Id)
            .OrderBy(x => x.Name)
            .ToListAsync();

        var classes = classEntities.Select((item, index) => new ClassOverview(
            item.Id,
            item.Name,
            item.Grade ?? "",
            item.Track ?? "",
            item.HomeTeacher?.FullName ?? "Chưa phân công",
            item.HomeTeacherId,
            item.Room ?? "Chưa chọn phòng",
            item.MaxStudents,
            item.Students.Count(x => x.IsActive),
            avg == 0 ? 8.0m + index * 0.1m : avg,
            index == 0)).ToList();

        var assignmentsQuery = db.Users.AsNoTracking()
            .Where(x => x.Role == UserRole.Student);
        if (!string.IsNullOrWhiteSpace(q))
        {
            assignmentsQuery = assignmentsQuery.Where(x => x.FullName.Contains(q) || x.Username.Contains(q) || x.Email.Contains(q));
        }

        var students = await assignmentsQuery
            .OrderBy(x => x.FullName)
            .ToListAsync();
        var studentProfiles = await db.StudentProfiles.AsNoTracking().ToDictionaryAsync(x => x.UserId, x => x.StudentCode);
        var classStudentRows = await db.ClassStudents.AsNoTracking()
            .Include(x => x.Class)
            .Where(x => x.Class != null && x.Class.AcademicYearId == academicYear.Id)
            .OrderByDescending(x => x.EnrolledAt)
            .ToListAsync();
        var classStudentMap = classStudentRows
            .GroupBy(x => x.StudentId)
            .ToDictionary(x => x.Key, x => x.First());
        var studentRows = students.Select(student =>
        {
            classStudentMap.TryGetValue(student.Id, out var assignment);
            return new StudentAssignmentRow(
                student,
                studentProfiles.GetValueOrDefault(student.Id, $"TS-{student.Id.ToString()[..4].ToUpperInvariant()}"),
                assignment?.ClassId,
                assignment?.Class?.Name ?? "Chưa xếp lớp",
                assignment?.IsActive ?? student.IsActive);
        }).ToList();

        return View(new ClassManagementViewModel(classes, studentRows, teachers, academicYears, academicYear.Id));
    }

    [HttpPost("/quan-tri/lop-hoc")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateClass(string name, string? grade, string? track, string? room, Guid? homeTeacherId, int maxStudents = 45)
    {
        var organization = await GetOrCreateOrganizationAsync();
        var academicYear = await GetOrCreateCurrentAcademicYearAsync(organization.Id);
        var existing = await db.Classes.FirstOrDefaultAsync(x => x.AcademicYearId == academicYear.Id && x.Name == name);
        if (existing is null)
        {
            db.Classes.Add(new SchoolClass
            {
                OrganizationId = organization.Id,
                AcademicYearId = academicYear.Id,
                Name = name,
                Grade = string.IsNullOrWhiteSpace(grade) ? new string(name.TakeWhile(char.IsDigit).ToArray()) : grade.Trim(),
                Track = track?.Trim(),
                Room = room,
                HomeTeacherId = homeTeacherId,
                MaxStudents = Math.Clamp(maxStudents, 1, 80)
            });
            await db.SaveChangesAsync();
            TempData["AdminMessage"] = $"Đã tạo lớp {name}.";
        }
        else
        {
            existing.Room = room;
            existing.Grade = string.IsNullOrWhiteSpace(grade) ? existing.Grade : grade.Trim();
            existing.Track = track?.Trim();
            existing.HomeTeacherId = homeTeacherId;
            existing.MaxStudents = Math.Clamp(maxStudents, 1, 80);
            await db.SaveChangesAsync();
            TempData["AdminMessage"] = $"Đã cập nhật lớp {name}.";
        }
        return RedirectToAction(nameof(Classes));
    }

    [HttpPost("/quan-tri/lop-hoc/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateClass(int id, string name, string? grade, string? track, string? room, Guid? homeTeacherId, int maxStudents = 45)
    {
        var schoolClass = await db.Classes.FindAsync(id);
        if (schoolClass is null) return NotFound();

        var duplicate = await db.Classes.AnyAsync(x => x.Id != id && x.AcademicYearId == schoolClass.AcademicYearId && x.Name == name);
        if (duplicate)
        {
            TempData["AdminMessage"] = $"Không thể đổi tên lớp thành {name} vì đã tồn tại trong năm học này.";
            return RedirectToAction(nameof(Classes));
        }

        schoolClass.Name = name.Trim();
        schoolClass.Grade = grade?.Trim();
        schoolClass.Track = track?.Trim();
        schoolClass.Room = room?.Trim();
        schoolClass.HomeTeacherId = homeTeacherId;
        schoolClass.MaxStudents = Math.Clamp(maxStudents, 1, 80);

        var roomRows = await db.ExamRooms.Where(x => x.ClassId == schoolClass.Id).ToListAsync();
        foreach (var roomRow in roomRows)
        {
            roomRow.ClassName = schoolClass.Name;
            roomRow.Room = schoolClass.Room ?? roomRow.Room;
        }

        await db.SaveChangesAsync();
        TempData["AdminMessage"] = $"Đã cập nhật lớp {schoolClass.Name}.";
        return RedirectToAction(nameof(Classes));
    }

    [HttpPost("/quan-tri/lop-hoc/chuyen-lop")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TransferStudent(Guid studentId, int classId)
    {
        var targetClass = await db.Classes.FindAsync(classId);
        if (targetClass is null) return NotFound();

        var currentAssignments = await db.ClassStudents.Where(x => x.StudentId == studentId).ToListAsync();
        db.ClassStudents.RemoveRange(currentAssignments);
        db.ClassStudents.Add(new ClassStudent
        {
            StudentId = studentId,
            ClassId = classId,
            EnrolledAt = DateTime.UtcNow,
            IsActive = true
        });
        await db.SaveChangesAsync();
        TempData["AdminMessage"] = $"Đã chuyển thí sinh sang lớp {targetClass.Name}.";
        return RedirectToAction(nameof(Classes));
    }

    [HttpPost("/quan-tri/lop-hoc/{id:int}/xoa")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteClass(int id)
    {
        var schoolClass = await db.Classes
            .Include(x => x.Students)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (schoolClass is null) return NotFound();
        if (schoolClass.Students.Any())
        {
            TempData["AdminMessage"] = $"Không thể xóa lớp {schoolClass.Name} vì vẫn còn thí sinh. Hãy chuyển thí sinh sang lớp khác trước.";
            return RedirectToAction(nameof(Classes));
        }

        var rooms = await db.ExamRooms.Where(x => x.ClassId == id).ToListAsync();
        foreach (var room in rooms)
        {
            room.ClassId = null;
            room.ClassName = $"{room.ClassName} (đã xóa)";
        }

        db.Classes.Remove(schoolClass);
        await db.SaveChangesAsync();
        TempData["AdminMessage"] = $"Đã xóa lớp {schoolClass.Name}.";
        return RedirectToAction(nameof(Classes));
    }

    [HttpPost("/quan-tri/lop-hoc/nhap")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportClasses(IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            TempData["AdminMessage"] = "Vui lòng chọn file CSV để import.";
            return RedirectToAction(nameof(Classes));
        }

        if (!UploadRules.IsAllowedImport(file, [".csv"], out var uploadError))
        {
            TempData["AdminMessage"] = uploadError;
            return RedirectToAction(nameof(Classes));
        }

        var organization = await GetOrCreateOrganizationAsync();
        var academicYear = await GetOrCreateCurrentAcademicYearAsync(organization.Id);
        var teachers = await db.Users.Where(x => x.Role == UserRole.Teacher).ToListAsync();
        var createdUsers = 0;
        var createdClasses = 0;
        var assignedStudents = 0;

        using var reader = new StreamReader(file.OpenReadStream());
        var lineNumber = 0;
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            lineNumber++;
            if (lineNumber > UploadRules.MaxImportRows + 1)
            {
                TempData["AdminMessage"] = $"Tệp có quá nhiều dòng, chỉ xử lý {UploadRules.MaxImportRows} dòng đầu.";
                break;
            }
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (lineNumber == 1 && line.Contains("username", StringComparison.OrdinalIgnoreCase)) continue;

            var cells = SplitCsvLine(line);
            if (cells.Count < 6) continue;

            var username = cells[0].Trim();
            var fullName = cells[1].Trim();
            var email = cells[2].Trim();
            var roleText = cells[3].Trim();
            var className = cells[4].Trim();
            var password = string.IsNullOrWhiteSpace(cells[5]) ? "Student@123" : cells[5].Trim();
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email)) continue;

            var role = Enum.TryParse<UserRole>(roleText, true, out var parsedRole) ? parsedRole : UserRole.Student;
            var user = await db.Users.FirstOrDefaultAsync(x => x.Username == username || x.Email == email);
            if (user is null)
            {
                user = new User
                {
                    Username = username,
                    Email = email,
                    FullName = fullName,
                    Role = role,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, 12)
                };
                db.Users.Add(user);
                createdUsers++;
                await db.SaveChangesAsync();
            }

            if (role == UserRole.Teacher && !await db.TeacherProfiles.AnyAsync(x => x.UserId == user.Id))
            {
                db.TeacherProfiles.Add(new TeacherProfile
                {
                    UserId = user.Id,
                    EmployeeCode = $"GV-{user.Id.ToString()[..4].ToUpperInvariant()}",
                    Specialization = "Chưa cập nhật",
                    Degree = "Chưa cập nhật"
                });
            }

            if (role != UserRole.Student || string.IsNullOrWhiteSpace(className)) continue;

            var schoolClass = await db.Classes.FirstOrDefaultAsync(x => x.AcademicYearId == academicYear.Id && x.Name == className);
            if (schoolClass is null)
            {
                schoolClass = new SchoolClass
                {
                    OrganizationId = organization.Id,
                    AcademicYearId = academicYear.Id,
                    Name = className,
                    Grade = new string(className.TakeWhile(char.IsDigit).ToArray()),
                    Room = "Chưa phân phòng",
                    HomeTeacherId = teachers.FirstOrDefault()?.Id
                };
                db.Classes.Add(schoolClass);
                createdClasses++;
                await db.SaveChangesAsync();
            }

            if (!await db.StudentProfiles.AnyAsync(x => x.UserId == user.Id))
            {
                db.StudentProfiles.Add(new StudentProfile
                {
                    UserId = user.Id,
                    StudentCode = $"TS-{user.Id.ToString()[..4].ToUpperInvariant()}",
                    Conduct = "Tốt"
                });
            }

            var oldAssignments = await db.ClassStudents.Where(x => x.StudentId == user.Id).ToListAsync();
            db.ClassStudents.RemoveRange(oldAssignments);
            db.ClassStudents.Add(new ClassStudent
            {
                ClassId = schoolClass.Id,
                StudentId = user.Id,
                EnrolledAt = DateTime.UtcNow,
                IsActive = true
            });
            assignedStudents++;
        }

        await db.SaveChangesAsync();
        TempData["AdminMessage"] = $"Import xong: tạo {createdUsers} tài khoản, {createdClasses} lớp, xếp lớp {assignedStudents} thí sinh.";
        return RedirectToAction(nameof(Classes));
    }

    [HttpGet("/quan-tri/lop-hoc/xuat")]
    public async Task<IActionResult> ExportClasses()
    {
        var assignments = await db.ClassStudents.AsNoTracking()
            .Include(x => x.Student)
            .Include(x => x.Class)
            .Where(x => x.Student != null)
            .OrderBy(x => x.Class!.Name)
            .ThenBy(x => x.Student!.FullName)
            .ToListAsync();
        var rows = new List<string> { "FullName,Username,Email,Class,Status" };
        rows.AddRange(assignments.Select(x => Csv([
            x.Student?.FullName,
            x.Student?.Username,
            x.Student?.Email,
            x.Class?.Name,
            x.IsActive ? "Active" : "Locked"
        ])));
        return File(System.Text.Encoding.UTF8.GetBytes(string.Join(Environment.NewLine, rows)), "text/csv; charset=utf-8", "quizarena-classes.csv");
    }

    [HttpGet("/quan-tri/giao-vien")]
    public async Task<IActionResult> Teachers()
    {
        var teachers = await db.Users.AsNoTracking()
            .Where(x => x.Role == UserRole.Teacher)
            .OrderBy(x => x.FullName)
            .ToListAsync();
        var questionCounts = await db.Questions.AsNoTracking()
            .GroupBy(x => x.CreatedBy)
            .Select(x => new { UserId = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count);
        var examCounts = await db.Exams.AsNoTracking()
            .GroupBy(x => x.CreatedBy)
            .Select(x => new { UserId = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count);
        var profileMap = await db.TeacherProfiles.AsNoTracking()
            .ToDictionaryAsync(x => x.UserId, x => x);
        var classCounts = await db.Classes.AsNoTracking()
            .Where(x => x.HomeTeacherId.HasValue)
            .GroupBy(x => x.HomeTeacherId!.Value)
            .Select(x => new { UserId = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count);

        var rows = teachers.Select((teacher, index) => new TeacherProfileRow(
            teacher,
            profileMap.TryGetValue(teacher.Id, out var profile) ? profile.EmployeeCode : $"GV-{teacher.Id.ToString()[..4].ToUpperInvariant()}",
            profileMap.TryGetValue(teacher.Id, out profile) ? profile.Specialization ?? "Chưa cập nhật" : (index % 2 == 0 ? "Toán học, Tin học" : "Vật lý, Hóa học"),
            profileMap.TryGetValue(teacher.Id, out profile) ? profile.Degree ?? "Chưa cập nhật" : (index % 2 == 0 ? "Thạc sĩ" : "Cử nhân"),
            questionCounts.GetValueOrDefault(teacher.Id),
            examCounts.GetValueOrDefault(teacher.Id),
            classCounts.GetValueOrDefault(teacher.Id))).ToList();

        return View(new TeacherProfilesViewModel(rows));
    }

    [HttpGet("/quan-tri/nam-hoc")]
    public async Task<IActionResult> AcademicYears()
    {
        var organization = await GetOrCreateOrganizationAsync();
        await GetOrCreateCurrentAcademicYearAsync(organization.Id);
        var exams = await db.Exams.AsNoTracking().ToListAsync();
        var semesters = await db.Semesters.AsNoTracking()
            .Include(x => x.AcademicYear)
            .OrderByDescending(x => x.AcademicYear!.Name)
            .ThenBy(x => x.StartDate)
            .ToListAsync();
        var terms = semesters.Select(x => new AcademicTermRow(
            x.AcademicYear?.Name ?? "-",
            x.Name,
            $"{x.StartDate:dd/MM/yyyy} - {x.EndDate:dd/MM/yyyy}",
            x.IsActive,
            exams.Count(e => DateOnly.FromDateTime(e.StartTime) >= x.StartDate && DateOnly.FromDateTime(e.StartTime) <= x.EndDate))).ToList();
        var model = new AcademicYearsViewModel(
            terms,
            [
                new("Năm học hiện tại", semesters.FirstOrDefault(x => x.AcademicYear?.IsCurrent == true)?.AcademicYear?.Name ?? "Chưa có", "đang dùng cho lớp và học bạ", "primary"),
                new("Học kỳ đang mở", terms.FirstOrDefault(x => x.IsActive)?.Semester ?? "Chưa chọn", "thiết lập toàn trường", "success"),
                new("Kỳ thi đã phân kỳ", exams.Count.ToString(), "lấy từ dữ liệu kỳ thi hiện có", "warning"),
                new("Lưu lịch sử điểm", "Bật", "sẵn sàng nối bảng học bạ", "purple")
            ]);
        return View(model);
    }

    [HttpPost("/quan-tri/nam-hoc")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAcademicTerm(string academicYear, string semester, DateTime startDate, DateTime endDate)
    {
        var organization = await GetOrCreateOrganizationAsync();
        var year = await db.AcademicYears.FirstOrDefaultAsync(x => x.OrganizationId == organization.Id && x.Name == academicYear);
        if (year is null)
        {
            year = new AcademicYear
            {
                OrganizationId = organization.Id,
                Name = academicYear,
                StartDate = DateOnly.FromDateTime(startDate),
                EndDate = DateOnly.FromDateTime(endDate)
            };
            db.AcademicYears.Add(year);
            await db.SaveChangesAsync();
        }

        var term = await db.Semesters.FirstOrDefaultAsync(x => x.AcademicYearId == year.Id && x.Name == semester);
        if (term is null)
        {
            db.Semesters.Add(new Semester
            {
                AcademicYearId = year.Id,
                Name = semester,
                StartDate = DateOnly.FromDateTime(startDate),
                EndDate = DateOnly.FromDateTime(endDate)
            });
        }
        else
        {
            term.StartDate = DateOnly.FromDateTime(startDate);
            term.EndDate = DateOnly.FromDateTime(endDate);
        }
        await db.SaveChangesAsync();
        TempData["AdminMessage"] = $"Đã lưu {semester} - {academicYear}.";
        return RedirectToAction(nameof(AcademicYears));
    }

    [HttpGet("/quan-tri/phong-thi")]
    public async Task<IActionResult> ExamRooms()
    {
        var organization = await GetOrCreateOrganizationAsync();
        var academicYear = await GetOrCreateCurrentAcademicYearAsync(organization.Id);
        if (!await db.ExamRooms.AnyAsync())
        {
            await AutoCreateExamRoomsAsync(academicYear.Id);
        }

        var exams = await db.Exams.AsNoTracking()
            .Include(x => x.Subject)
            .OrderByDescending(x => x.StartTime)
            .ToListAsync();
        var classes = await db.Classes.AsNoTracking()
            .Where(x => x.AcademicYearId == academicYear.Id)
            .OrderBy(x => x.Name)
            .ToListAsync();
        var teachers = await db.Users.AsNoTracking()
            .Where(x => x.Role == UserRole.Teacher)
            .OrderBy(x => x.FullName)
            .ToListAsync();

        var savedRooms = await db.ExamRooms.AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
        var rooms = savedRooms.Select(x => new ExamRoomRow(
            x.Id,
            x.ExamId,
            x.ClassId,
            x.ProctorId,
            x.ExamName,
            x.ClassName,
            x.Room,
            x.Shift,
            x.ProctorName ?? "Chưa phân công",
            x.StudentCount,
            x.Status)).ToList();

        return View(new ExamRoomsViewModel(rooms, exams, classes, teachers));
    }

    private async Task AutoCreateExamRoomsAsync(int academicYearId)
    {
        var exams = await db.Exams.AsNoTracking()
            .Include(x => x.Subject)
            .OrderBy(x => x.StartTime)
            .Take(12)
            .ToListAsync();
        var teachers = await db.Users.AsNoTracking()
            .Where(x => x.Role == UserRole.Teacher)
            .OrderBy(x => x.FullName)
            .ToListAsync();
        var classes = await db.Classes.AsNoTracking()
            .Include(x => x.Students)
            .Where(x => x.AcademicYearId == academicYearId)
            .OrderBy(x => x.Name)
            .ToListAsync();

        if (classes.Count == 0)
        {
            return;
        }

        foreach (var (exam, index) in exams.Select((exam, index) => (exam, index)))
        {
            var schoolClass = classes[index % classes.Count];
            var teacher = teachers.Count == 0 ? null : teachers[index % teachers.Count];
            if (await db.ExamRooms.AnyAsync(x => x.ExamId == exam.Id && x.ClassId == schoolClass.Id))
            {
                continue;
            }

            db.ExamRooms.Add(new ExamRoom
            {
                ExamId = exam.Id,
                ClassId = schoolClass.Id,
                ExamName = exam.Title,
                ClassName = schoolClass.Name,
                Room = schoolClass.Room ?? $"Phòng {301 + index}",
                Shift = $"{exam.StartTime.ToLocalTime():dd/MM HH:mm}",
                ProctorId = teacher?.Id,
                ProctorName = teacher?.FullName,
                StudentCount = Math.Max(schoolClass.Students.Count(x => x.IsActive), 1),
                Status = exam.StartTime > DateTime.UtcNow ? "Sắp diễn ra" : exam.EndTime < DateTime.UtcNow ? "Đã kết thúc" : "Đang thi"
            });
        }

        await db.SaveChangesAsync();
    }

    [HttpPost("/quan-tri/phong-thi/tu-dong-xep")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AutoAssignExamRooms()
    {
        var organization = await GetOrCreateOrganizationAsync();
        var academicYear = await GetOrCreateCurrentAcademicYearAsync(organization.Id);
        await AutoCreateExamRoomsAsync(academicYear.Id);
        TempData["AdminMessage"] = "Đã tự chia phòng dựa trên kỳ thi và danh sách lớp hiện có.";
        return RedirectToAction(nameof(ExamRooms));
    }

    [HttpPost("/quan-tri/phong-thi")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateExamRoom(int examId, int classId, string room, string shift, Guid? proctorId, string status = "Sắp diễn ra")
    {
        var exam = await db.Exams.AsNoTracking().FirstOrDefaultAsync(x => x.Id == examId);
        var schoolClass = await db.Classes.AsNoTracking().Include(x => x.Students).FirstOrDefaultAsync(x => x.Id == classId);
        var proctor = proctorId.HasValue ? await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == proctorId.Value && x.Role == UserRole.Teacher) : null;
        if (exam is null || schoolClass is null)
        {
            TempData["AdminMessage"] = "Vui lòng chọn kỳ thi và lớp hợp lệ.";
            return RedirectToAction(nameof(ExamRooms));
        }

        db.ExamRooms.Add(new ExamRoom
        {
            ExamId = exam.Id,
            ClassId = schoolClass.Id,
            ExamName = exam.Title,
            ClassName = schoolClass.Name,
            Room = room,
            Shift = shift,
            ProctorId = proctor?.Id,
            ProctorName = proctor?.FullName,
            StudentCount = Math.Max(schoolClass.Students.Count(x => x.IsActive), 1),
            Status = status
        });
        await db.SaveChangesAsync();
        TempData["AdminMessage"] = $"Đã lưu ca thi {exam.Title} - {schoolClass.Name} tại {room}.";
        return RedirectToAction(nameof(ExamRooms));
    }

    [HttpPost("/quan-tri/phong-thi/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateExamRoom(int id, int examId, int classId, string room, string shift, Guid? proctorId, string status)
    {
        var examRoom = await db.ExamRooms.FindAsync(id);
        if (examRoom is null) return NotFound();
        var exam = await db.Exams.AsNoTracking().FirstOrDefaultAsync(x => x.Id == examId);
        var schoolClass = await db.Classes.AsNoTracking().Include(x => x.Students).FirstOrDefaultAsync(x => x.Id == classId);
        var proctor = proctorId.HasValue ? await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == proctorId.Value && x.Role == UserRole.Teacher) : null;
        if (exam is null || schoolClass is null)
        {
            TempData["AdminMessage"] = "Vui lòng chọn kỳ thi và lớp hợp lệ.";
            return RedirectToAction(nameof(ExamRooms));
        }

        examRoom.ExamId = exam.Id;
        examRoom.ExamName = exam.Title;
        examRoom.ClassId = schoolClass.Id;
        examRoom.ClassName = schoolClass.Name;
        examRoom.Room = room.Trim();
        examRoom.Shift = shift.Trim();
        examRoom.ProctorId = proctor?.Id;
        examRoom.ProctorName = proctor?.FullName;
        examRoom.StudentCount = Math.Max(schoolClass.Students.Count(x => x.IsActive), 1);
        examRoom.Status = status;
        await db.SaveChangesAsync();
        TempData["AdminMessage"] = "Đã cập nhật ca thi.";
        return RedirectToAction(nameof(ExamRooms));
    }

    [HttpGet("/quan-tri/cai-dat")]
    public async Task<IActionResult> Settings()
    {
        var organization = await GetOrCreateOrganizationAsync();
        var storedLogo = organization.LogoUrl;
        ViewBag.Organization = organization;
        ViewBag.LogoUrl = !string.IsNullOrWhiteSpace(storedLogo) ? storedLogo : FindOrganizationLogo();
        return View();
    }

    [HttpPost("/quan-tri/cai-dat")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveOrganizationInfo(
        string name,
        string code,
        string? address,
        string? phone,
        string? email,
        string? website,
        string? description)
    {
        var organization = await GetOrCreateOrganizationAsync();
        organization.Name = string.IsNullOrWhiteSpace(name) ? organization.Name : name.Trim();
        organization.Code = string.IsNullOrWhiteSpace(code) ? organization.Code : code.Trim();
        organization.Address = address?.Trim();
        organization.Phone = phone?.Trim();
        organization.Email = email?.Trim();
        organization.Website = website?.Trim();
        organization.Description = description?.Trim();
        await db.SaveChangesAsync();
        cache.Remove("school_name");

        TempData["SettingsMessage"] = "Đã lưu thông tin tổ chức.";
        return RedirectToAction(nameof(Settings));
    }

    [HttpPost("/quan-tri/cai-dat/tinh-nang")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSettings(
        bool allowViewAnswerAfterSubmit,
        bool autoSubmitOnTimeout,
        bool antiCheatTabSwitch,
        bool shuffleQuestions,
        bool shuffleAnswers,
        bool shareQuestionBank,
        bool sendEmailOnResult)
    {
        var organization = await GetOrCreateOrganizationAsync();
        organization.AllowViewAnswerAfterSubmit = allowViewAnswerAfterSubmit;
        organization.AutoSubmitOnTimeout = autoSubmitOnTimeout;
        organization.AntiCheatTabSwitch = antiCheatTabSwitch;
        organization.ShuffleQuestions = shuffleQuestions;
        organization.ShuffleAnswers = shuffleAnswers;
        organization.ShareQuestionBank = shareQuestionBank;
        organization.SendEmailOnResult = sendEmailOnResult;
        await db.SaveChangesAsync();

        TempData["SettingsMessage"] = "Đã lưu cấu hình tổ chức.";
        return RedirectToAction(nameof(Settings));
    }

    [HttpPost("/quan-tri/cai-dat/logo")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadLogo(IFormFile? logo)
    {
        if (logo is null || logo.Length == 0)
        {
            TempData["SettingsMessage"] = "Vui lòng chọn file ảnh logo.";
            return RedirectToAction(nameof(Settings));
        }

        var ext = Path.GetExtension(logo.FileName).ToLowerInvariant();
        var allowed = new HashSet<string> { ".jpg", ".jpeg", ".png", ".webp" };
        if (!allowed.Contains(ext))
        {
            TempData["SettingsMessage"] = "Logo chỉ hỗ trợ JPG, PNG hoặc WEBP.";
            return RedirectToAction(nameof(Settings));
        }

        if (logo.Length > 2 * 1024 * 1024)
        {
            TempData["SettingsMessage"] = "Logo cần nhỏ hơn 2MB.";
            return RedirectToAction(nameof(Settings));
        }

        if (!await UploadRules.LooksLikeImageAsync(logo))
        {
            TempData["SettingsMessage"] = "Tệp không phải ảnh JPG, PNG hoặc WEBP hợp lệ.";
            return RedirectToAction(nameof(Settings));
        }

        var uploadDir = Path.Combine(env.WebRootPath, "uploads");
        Directory.CreateDirectory(uploadDir);
        foreach (var oldLogo in Directory.EnumerateFiles(uploadDir, "organization-logo.*"))
        {
            System.IO.File.Delete(oldLogo);
        }

        var fileName = $"organization-logo{ext}";
        await using var stream = System.IO.File.Create(Path.Combine(uploadDir, fileName));
        await logo.CopyToAsync(stream);
        var organization = await GetOrCreateOrganizationAsync();
        organization.LogoUrl = $"/uploads/{fileName}";
        await db.SaveChangesAsync();
        TempData["SettingsMessage"] = "Đã cập nhật logo tổ chức.";
        return RedirectToAction(nameof(Settings));
    }

    private string? FindOrganizationLogo()
    {
        var uploadDir = Path.Combine(env.WebRootPath, "uploads");
        if (!Directory.Exists(uploadDir)) return null;
        var logo = Directory.EnumerateFiles(uploadDir, "organization-logo.*").FirstOrDefault();
        return logo is null ? null : $"/uploads/{Path.GetFileName(logo)}";
    }

    private async Task<Organization> GetOrCreateOrganizationAsync()
    {
        var organization = await db.Organizations.FirstOrDefaultAsync();
        if (organization is not null) return organization;

        organization = new Organization
        {
            Name = "THPT Chu Văn An",
            Code = "THPT-CVA-HN01",
            Address = "Đường Thụy Khuê, Tây Hồ, Hà Nội",
            Phone = "024 3823 1234",
            Email = "contact@chuvan.edu.vn",
            Website = "https://thptchuvan.edu.vn",
            Description = "Trường THPT Chu Văn An - Hà Nội. Đơn vị trực thuộc Sở GD&ĐT Hà Nội."
        };
        db.Organizations.Add(organization);
        await db.SaveChangesAsync();
        return organization;
    }

    private async Task<AcademicYear> GetOrCreateCurrentAcademicYearAsync(int organizationId)
    {
        var academicYear = await db.AcademicYears.FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.IsCurrent);
        if (academicYear is not null) return academicYear;

        academicYear = new AcademicYear
        {
            OrganizationId = organizationId,
            Name = "2025-2026",
            StartDate = new DateOnly(2025, 8, 15),
            EndDate = new DateOnly(2026, 5, 31),
            IsCurrent = true
        };
        db.AcademicYears.Add(academicYear);
        await db.SaveChangesAsync();

        if (!await db.Semesters.AnyAsync(x => x.AcademicYearId == academicYear.Id))
        {
            db.Semesters.AddRange(
                new Semester { AcademicYearId = academicYear.Id, Name = "Học kỳ 1", StartDate = new DateOnly(2025, 8, 15), EndDate = new DateOnly(2025, 12, 31) },
                new Semester { AcademicYearId = academicYear.Id, Name = "Học kỳ 2", StartDate = new DateOnly(2026, 1, 5), EndDate = new DateOnly(2026, 5, 31), IsActive = true });
            await db.SaveChangesAsync();
        }

        return academicYear;
    }

    private static string Palette(int index)
    {
        var colors = new[] { "#3B82F6", "#22C55E", "#F97316", "#94A3B8", "#0D9488", "#EF4444" };
        return colors[index % colors.Length];
    }

    private static IReadOnlyList<ActivityItem> BuildActivities(IReadOnlyList<Exam> exams, IReadOnlyList<ExamAttempt> attempts, IReadOnlyList<User> users)
    {
        var activities = new List<ActivityItem>();
        activities.AddRange(attempts.Take(4).Select(x => new ActivityItem(
            "fa-clipboard-check",
            $"{x.User?.FullName ?? "Thí sinh"} {(x.SubmittedAt.HasValue ? "đã nộp bài" : "bắt đầu làm bài")} {x.Exam?.Title}",
            RelativeTime(x.SubmittedAt ?? x.StartedAt),
            x.SubmittedAt.HasValue ? "success" : "primary")));
        activities.AddRange(exams.Take(2).Select(x => new ActivityItem(
            "fa-calendar-days",
            $"Kỳ thi {x.Title} được lên lịch cho môn {x.Subject?.Name}",
            RelativeTime(x.CreatedAt),
            "warning")));
        activities.AddRange(users.OrderByDescending(x => x.CreatedAt).Take(2).Select(x => new ActivityItem(
            "fa-user-plus",
            $"Tài khoản {x.FullName} được tạo",
            RelativeTime(x.CreatedAt),
            "primary")));
        return activities.Count == 0
            ? [new ActivityItem("fa-circle-info", "Chưa có hoạt động mới trong hệ thống.", "vừa xong", "primary")]
            : activities.Take(8).ToList();
    }

    private static string RelativeTime(DateTime utc)
    {
        var span = DateTime.UtcNow - utc;
        if (span.TotalMinutes < 1) return "vừa xong";
        if (span.TotalHours < 1) return $"{Math.Floor(span.TotalMinutes)} phút trước";
        if (span.TotalDays < 1) return $"{Math.Floor(span.TotalHours)} giờ trước";
        return $"{Math.Floor(span.TotalDays)} ngày trước";
    }

    private static string Csv(IEnumerable<string?> values)
    {
        return string.Join(",", values.Select(value =>
        {
            var text = UploadRules.SafeCell(value);
            return $"\"{text.Replace("\"", "\"\"")}\"";
        }));
    }

    private static List<string> SplitCsvLine(string line)
    {
        var values = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
                continue;
            }

            if (ch == ',' && !inQuotes)
            {
                values.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        values.Add(current.ToString());
        return values;
    }
}
