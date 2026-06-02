using ExamHub.Core.Entities;
using ExamHub.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace ExamHub.Infrastructure.Data;

public static class SeedData
{
    public static async Task EnsureSeededAsync(ExamHubDbContext db, CancellationToken cancellationToken = default)
    {
        if (await db.Users.AnyAsync(cancellationToken)) return;

        var admin = NewUser("admin_root", "admin@examhub.vn", "Nguyễn Admin", "Admin@123456", UserRole.Admin);
        var teacher = NewUser("gv_tranvan", "gv.tranvan@examhub.vn", "Trần Văn Giáo", "Teacher@123", UserRole.Teacher);
        var student1 = NewUser("ts_nguyen01", "ts.nguyen01@student.vn", "Nguyễn Thí Sinh", "Student@123", UserRole.Student);
        var student2 = NewUser("ts_lehoang", "ts.lehoang@student.vn", "Lê Hoàng Sinh", "Student@123", UserRole.Student);
        db.Users.AddRange(admin, teacher, student1, student2);

        var algebra = new Subject { Name = "Đại số tuyến tính", Description = "Ma trận, định thức, không gian vector", CreatedBy = teacher.Id };
        var web = new Subject { Name = "Lập trình Web", Description = "HTML, CSS, ASP.NET Core MVC", CreatedBy = teacher.Id };
        db.Subjects.AddRange(algebra, web);
        await db.SaveChangesAsync(cancellationToken);

        db.Questions.AddRange(BuildAlgebraQuestions(algebra.Id, teacher.Id));
        await db.SaveChangesAsync(cancellationToken);

        var now = DateTime.UtcNow;
        db.Exams.Add(new Exam
        {
            SubjectId = algebra.Id,
            Title = "Đại số - Giữa kỳ 2026",
            Description = "Đề thi sinh tự động từ ngân hàng câu hỏi",
            DurationMinutes = 60,
            GenerateMode = GenerateMode.ByCount,
            QuestionCount = 10,
            StartTime = now,
            EndTime = now.AddDays(7),
            CreatedBy = teacher.Id
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private static User NewUser(string username, string email, string fullName, string password, UserRole role) => new()
    {
        Username = username,
        Email = email,
        FullName = fullName,
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12),
        Role = role
    };

    private static List<Question> BuildAlgebraQuestions(int subjectId, Guid teacherId)
    {
        var data = new List<Question>();

        for (var i = 1; i <= 10; i++)
        {
            data.Add(new Question
            {
                SubjectId = subjectId,
                CreatedBy = teacherId,
                Type = QuestionType.SingleChoice,
                Difficulty = i <= 5 ? Difficulty.Easy : Difficulty.Medium,
                Content = i == 1 ? "Cho ma trận A có det(A) = 0. Điều nào sau đây đúng?" : $"Câu một đáp án {i}: Tính chất cơ bản của ma trận vuông là gì?",
                Answers =
                {
                    new Answer { Content = "A là ma trận đơn vị", OrderIndex = 1 },
                    new Answer { Content = i == 1 ? "A không khả nghịch" : "Định thức của ma trận đơn vị bằng 1", IsCorrect = true, OrderIndex = 2 },
                    new Answer { Content = "A luôn có nghịch đảo", OrderIndex = 3 },
                    new Answer { Content = "det(A) luôn âm", OrderIndex = 4 }
                }
            });
        }

        for (var i = 1; i <= 5; i++)
        {
            data.Add(new Question
            {
                SubjectId = subjectId,
                CreatedBy = teacherId,
                Type = QuestionType.MultipleChoice,
                Difficulty = i <= 3 ? Difficulty.Medium : Difficulty.Hard,
                Points = 1.5m,
                Content = i == 1 ? "Các tính chất nào sau đây luôn đúng với mọi ma trận vuông A, B?" : $"Câu nhiều đáp án {i}: Chọn các mệnh đề đúng về phép toán ma trận.",
                Answers =
                {
                    new Answer { Content = "det(AB) = det(A)·det(B)", IsCorrect = true, OrderIndex = 1 },
                    new Answer { Content = "AB = BA với mọi A, B", OrderIndex = 2 },
                    new Answer { Content = "tr(A+B) = tr(A)+tr(B)", IsCorrect = true, OrderIndex = 3 },
                    new Answer { Content = "det(A+B) = det(A)+det(B)", OrderIndex = 4 }
                }
            });
        }

        var textAnswers = new[] { ("Tính trace của ma trận A = [[2,1],[5,3]] bằng bao nhiêu?", "5"), ("Số chiều của R3 là bao nhiêu?", "3"), ("det(I2) bằng bao nhiêu?", "1"), ("Hạng tối đa của ma trận 2x3 là bao nhiêu?", "2"), ("Trace của ma trận không 2x2 bằng bao nhiêu?", "0") };
        foreach (var (content, answer) in textAnswers)
        {
            data.Add(new Question
            {
                SubjectId = subjectId,
                CreatedBy = teacherId,
                Type = QuestionType.TextAnswer,
                Difficulty = Difficulty.Easy,
                Content = content,
                Answers = { new Answer { Content = answer, IsCorrect = true, OrderIndex = 1 } }
            });
        }

        return data;
    }
}
