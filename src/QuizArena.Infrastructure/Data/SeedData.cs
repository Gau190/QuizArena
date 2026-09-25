using QuizArena.Core.Entities;
using QuizArena.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace QuizArena.Infrastructure.Data;

public static class SeedData
{
    public static async Task EnsureSeededAsync(QuizArenaDbContext db, CancellationToken cancellationToken = default)
    {
        if (await db.Users.AnyAsync(cancellationToken))
        {
            await EnsureV2SeededAsync(db, cancellationToken);
            return;
        }

        var admin = NewUser("admin_root", "admin@quizarena.vn", "Nguyễn Admin", "Admin@123456", UserRole.Admin);
        var teacher = NewUser("gv_tranvan", "gv.tranvan@quizarena.vn", "Trần Văn Giáo", "Teacher@123", UserRole.Teacher);
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
            MaxAttempts = 3,
            AllowViewAnswer = true,
            StartTime = now,
            EndTime = now.AddDays(7),
            CreatedBy = teacher.Id
        });
        await db.SaveChangesAsync(cancellationToken);
        await EnsureV2SeededAsync(db, cancellationToken);
    }

    private static async Task EnsureV2SeededAsync(QuizArenaDbContext db, CancellationToken cancellationToken)
    {
        var organization = await db.Organizations.FirstOrDefaultAsync(cancellationToken);
        if (organization is null)
        {
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
            await db.SaveChangesAsync(cancellationToken);
        }

        var academicYear = await db.AcademicYears.FirstOrDefaultAsync(x => x.IsCurrent, cancellationToken);
        if (academicYear is null)
        {
            academicYear = new AcademicYear
            {
                OrganizationId = organization.Id,
                Name = "2025-2026",
                StartDate = new DateOnly(2025, 8, 15),
                EndDate = new DateOnly(2026, 5, 31),
                IsCurrent = true
            };
            db.AcademicYears.Add(academicYear);
            await db.SaveChangesAsync(cancellationToken);
        }

        if (!await db.Semesters.AnyAsync(cancellationToken))
        {
            db.Semesters.AddRange(
                new Semester { AcademicYearId = academicYear.Id, Name = "Học kỳ 1", StartDate = new DateOnly(2025, 8, 15), EndDate = new DateOnly(2025, 12, 31) },
                new Semester { AcademicYearId = academicYear.Id, Name = "Học kỳ 2", StartDate = new DateOnly(2026, 1, 5), EndDate = new DateOnly(2026, 5, 31), IsActive = true });
            await db.SaveChangesAsync(cancellationToken);
        }

        var teachers = await db.Users.Where(x => x.Role == UserRole.Teacher).OrderBy(x => x.FullName).ToListAsync(cancellationToken);
        var students = await db.Users.Where(x => x.Role == UserRole.Student).OrderBy(x => x.FullName).ToListAsync(cancellationToken);
        var teacher = teachers.FirstOrDefault();

        foreach (var user in teachers)
        {
            if (!await db.TeacherProfiles.AnyAsync(x => x.UserId == user.Id, cancellationToken))
            {
                db.TeacherProfiles.Add(new TeacherProfile
                {
                    UserId = user.Id,
                    EmployeeCode = $"GV-{user.Id.ToString()[..4].ToUpperInvariant()}",
                    Specialization = "Toán học, Tin học",
                    Degree = "Thạc sĩ",
                    JoinDate = new DateOnly(2024, 8, 1)
                });
            }
        }

        foreach (var user in students)
        {
            if (!await db.StudentProfiles.AnyAsync(x => x.UserId == user.Id, cancellationToken))
            {
                db.StudentProfiles.Add(new StudentProfile
                {
                    UserId = user.Id,
                    StudentCode = $"TS-{user.Id.ToString()[..4].ToUpperInvariant()}",
                    ParentName = "Nguyễn Văn Hùng",
                    ParentPhone = "0912 345 678",
                    Conduct = "Tốt",
                    AcademicLevel = "Giỏi"
                });
            }
        }
        await db.SaveChangesAsync(cancellationToken);

        var defaultClass = await db.Classes.FirstOrDefaultAsync(x => x.Name == "12A1" && x.AcademicYearId == academicYear.Id, cancellationToken);
        if (defaultClass is null)
        {
            defaultClass = new SchoolClass
            {
                OrganizationId = organization.Id,
                AcademicYearId = academicYear.Id,
                Name = "12A1",
                Grade = "12",
                Track = "Khoa học tự nhiên",
                Room = "Phòng 301",
                HomeTeacherId = teacher?.Id
            };
            db.Classes.Add(defaultClass);
            await db.SaveChangesAsync(cancellationToken);
        }

        foreach (var student in students)
        {
            if (!await db.ClassStudents.AnyAsync(x => x.ClassId == defaultClass.Id && x.StudentId == student.Id, cancellationToken))
            {
                db.ClassStudents.Add(new ClassStudent { ClassId = defaultClass.Id, StudentId = student.Id });
            }
        }

        if (!await db.Notifications.AnyAsync(cancellationToken))
        {
            var admin = await db.Users.FirstOrDefaultAsync(x => x.Role == UserRole.Admin, cancellationToken);
            db.Notifications.AddRange(
                new Notification { Title = "Lịch thi mới được công bố", Body = "Kỳ thi Đại số - Giữa kỳ 2026 đã mở trong hệ thống.", Category = "Kỳ thi", CreatedBy = admin?.Id },
                new Notification { Title = "Có kết quả bài thi mới", Body = "Kết quả các lượt thi đã nộp được cập nhật trong trang báo cáo và học bạ điện tử.", Category = "Kết quả", CreatedBy = admin?.Id },
                new Notification { Title = "Câu hỏi cần rà soát", Body = "Một số câu hỏi có tỷ lệ sai cao, giáo viên nên kiểm tra lại đáp án.", Category = "Cảnh báo", CreatedBy = admin?.Id });
        }

        if (!await db.ExamRooms.AnyAsync(cancellationToken))
        {
            var exam = await db.Exams.OrderBy(x => x.StartTime).FirstOrDefaultAsync(cancellationToken);
            db.ExamRooms.Add(new ExamRoom
            {
                ExamId = exam?.Id,
                ClassId = defaultClass.Id,
                ExamName = exam?.Title ?? "Đại số - Giữa kỳ 2026",
                ClassName = defaultClass.Name,
                Room = defaultClass.Room ?? "Phòng 301",
                Shift = exam is null ? "08:00 - 09:00" : $"{exam.StartTime.ToLocalTime():dd/MM HH:mm}",
                ProctorId = teacher?.Id,
                ProctorName = teacher?.FullName,
                StudentCount = Math.Max(students.Count, 1),
                Status = "Sắp diễn ra"
            });
        }

        if (!await db.ExamBlueprintItems.AnyAsync(cancellationToken) && teacher is not null)
        {
            var subject = await db.Subjects.OrderBy(x => x.Name).FirstOrDefaultAsync(cancellationToken);
            db.ExamBlueprintItems.AddRange(
                new ExamBlueprintItem { SubjectId = subject?.Id, Chapter = "Chương 1 - Nhận biết nền tảng", EasyCount = 4, MediumCount = 2, Points = 3, CreatedBy = teacher.Id },
                new ExamBlueprintItem { SubjectId = subject?.Id, Chapter = "Chương 2 - Vận dụng", EasyCount = 2, MediumCount = 4, HardCount = 1, Points = 4, CreatedBy = teacher.Id },
                new ExamBlueprintItem { SubjectId = subject?.Id, Chapter = "Chương 3 - Tổng hợp", EasyCount = 1, MediumCount = 2, HardCount = 2, Points = 3, CreatedBy = teacher.Id });
        }

        if (!await db.QuestionReports.AnyAsync(cancellationToken))
        {
            var firstQuestion = await db.Questions.OrderBy(x => x.Id).FirstOrDefaultAsync(cancellationToken);
            var reporter = students.FirstOrDefault();
            if (firstQuestion is not null)
            {
                db.QuestionReports.Add(new QuestionReport
                {
                    QuestionId = firstQuestion.Id,
                    ReportedByUserId = reporter?.Id,
                    Reason = "Câu hỏi chưa rõ",
                    Status = "Pending"
                });
            }
        }

        await EnsureExtendedSampleDataAsync(db, organization, academicYear, cancellationToken);
        await EnsureExamClassesAsync(db, cancellationToken);
        await ResetAttemptsOnOpenExamsAsync(db, cancellationToken);
    }

    private static async Task EnsureExtendedSampleDataAsync(QuizArenaDbContext db, Organization organization, AcademicYear academicYear, CancellationToken cancellationToken)
    {
        var teachers = await EnsureSampleUsersAsync(db, SampleTeachers(), UserRole.Teacher, "Teacher@123", cancellationToken);
        var students = await EnsureSampleUsersAsync(db, SampleStudents(), UserRole.Student, "Student@123", cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        teachers = await db.Users.Where(x => x.Role == UserRole.Teacher).OrderBy(x => x.FullName).ToListAsync(cancellationToken);
        students = await db.Users.Where(x => x.Role == UserRole.Student).OrderBy(x => x.FullName).ToListAsync(cancellationToken);
        if (teachers.Count == 0 || students.Count == 0) return;

        await EnsureProfilesAsync(db, teachers, students, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        await EnsureClassesAsync(db, organization, academicYear, teachers, students, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        var subjects = await EnsureSubjectsAsync(db, teachers, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        subjects = await db.Subjects.Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync(cancellationToken);

        await EnsureQuestionsAsync(db, subjects, teachers, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        var exams = await EnsureExamsAsync(db, subjects, teachers, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        await EnsureAttemptsAsync(db, exams, students, cancellationToken);
        await EnsureMoreNotificationsAsync(db, teachers.First().Id, cancellationToken);
        await EnsureMoreExamRoomsAsync(db, academicYear, exams, teachers, cancellationToken);
        await EnsureMoreBlueprintsAsync(db, subjects, teachers.First().Id, cancellationToken);
        await EnsureMoreQuestionReportsAsync(db, students, cancellationToken);
    }

    private static async Task<List<User>> EnsureSampleUsersAsync(QuizArenaDbContext db, SeedUser[] users, UserRole role, string password, CancellationToken cancellationToken)
    {
        foreach (var user in users)
        {
            if (!await db.Users.AnyAsync(x => x.Username == user.Username || x.Email == user.Email, cancellationToken))
            {
                db.Users.Add(NewUser(user.Username, user.Email, user.FullName, password, role));
            }
        }

        var usernames = users.Select(x => x.Username).ToArray();
        return await db.Users.Where(x => usernames.Contains(x.Username)).OrderBy(x => x.FullName).ToListAsync(cancellationToken);
    }

    private static async Task EnsureProfilesAsync(QuizArenaDbContext db, IReadOnlyList<User> teachers, IReadOnlyList<User> students, CancellationToken cancellationToken)
    {
        var specialties = new[] { "Toán học", "Vật lý", "Hóa học", "Ngữ văn", "Tiếng Anh", "Tin học", "Sinh học", "Lịch sử" };
        for (var i = 0; i < teachers.Count; i++)
        {
            var teacher = teachers[i];
            if (!await db.TeacherProfiles.AnyAsync(x => x.UserId == teacher.Id, cancellationToken))
            {
                db.TeacherProfiles.Add(new TeacherProfile
                {
                    UserId = teacher.Id,
                    EmployeeCode = $"GV-{teacher.Id.ToString()[..4].ToUpperInvariant()}",
                    Specialization = specialties[i % specialties.Length],
                    Degree = i % 3 == 0 ? "Thạc sĩ" : "Cử nhân",
                    JoinDate = new DateOnly(2022 + i % 4, 8, 1)
                });
            }
        }

        for (var i = 0; i < students.Count; i++)
        {
            var student = students[i];
            if (!await db.StudentProfiles.AnyAsync(x => x.UserId == student.Id, cancellationToken))
            {
                db.StudentProfiles.Add(new StudentProfile
                {
                    UserId = student.Id,
                    StudentCode = $"HS{2026}{(i + 1):D3}",
                    Gender = i % 2 == 0 ? "Nam" : "Nữ",
                    DateOfBirth = new DateOnly(2008 + i % 3, 2 + i % 10, 5 + i % 20),
                    ParentName = SampleParents()[i % SampleParents().Length],
                    ParentPhone = $"09{(12000000 + i * 72391) % 100000000:D8}",
                    ParentEmail = $"phuhuynh{i + 1:00}@example.vn",
                    Address = $"{12 + i} Nguyễn Trãi, Hà Nội",
                    Conduct = i % 5 == 0 ? "Khá" : "Tốt",
                    AcademicLevel = i % 6 == 0 ? "Khá" : "Giỏi"
                });
            }
        }
    }

    private static async Task EnsureClassesAsync(QuizArenaDbContext db, Organization organization, AcademicYear academicYear, IReadOnlyList<User> teachers, IReadOnlyList<User> students, CancellationToken cancellationToken)
    {
        var classSeeds = new[]
        {
            new { Name = "10A1", Grade = "10", Room = "Phòng 201", Track = "Cơ bản" },
            new { Name = "10A2", Grade = "10", Room = "Phòng 202", Track = "Tiếng Anh tăng cường" },
            new { Name = "11A1", Grade = "11", Room = "Phòng 251", Track = "Khoa học tự nhiên" },
            new { Name = "11A2", Grade = "11", Room = "Phòng 252", Track = "Khoa học xã hội" },
            new { Name = "12A1", Grade = "12", Room = "Phòng 301", Track = "Khoa học tự nhiên" },
            new { Name = "12A2", Grade = "12", Room = "Phòng 302", Track = "Ôn thi tốt nghiệp" }
        };

        foreach (var (seed, index) in classSeeds.Select((seed, index) => (seed, index)))
        {
            var schoolClass = await db.Classes.FirstOrDefaultAsync(x => x.AcademicYearId == academicYear.Id && x.Name == seed.Name, cancellationToken);
            if (schoolClass is null)
            {
                schoolClass = new SchoolClass
                {
                    OrganizationId = organization.Id,
                    AcademicYearId = academicYear.Id,
                    Name = seed.Name,
                    Grade = seed.Grade,
                    Track = seed.Track,
                    Room = seed.Room,
                    HomeTeacherId = teachers[index % teachers.Count].Id
                };
                db.Classes.Add(schoolClass);
                await db.SaveChangesAsync(cancellationToken);
            }
            else
            {
                schoolClass.Track ??= seed.Track;
                schoolClass.Room ??= seed.Room;
                schoolClass.HomeTeacherId ??= teachers[index % teachers.Count].Id;
            }
        }

        var classes = await db.Classes.Where(x => x.AcademicYearId == academicYear.Id).OrderBy(x => x.Name).ToListAsync(cancellationToken);
        for (var i = 0; i < students.Count; i++)
        {
            var student = students[i];
            if (await db.ClassStudents.AnyAsync(x => x.StudentId == student.Id, cancellationToken)) continue;

            var schoolClass = classes[i % classes.Count];
            db.ClassStudents.Add(new ClassStudent
            {
                ClassId = schoolClass.Id,
                StudentId = student.Id,
                EnrolledAt = DateTime.UtcNow.AddDays(-90 + i)
            });
        }
    }

    private static async Task<List<Subject>> EnsureSubjectsAsync(QuizArenaDbContext db, IReadOnlyList<User> teachers, CancellationToken cancellationToken)
    {
        var subjectSeeds = new[]
        {
            ("Đại số tuyến tính", "Ma trận, định thức, không gian vector"),
            ("Lập trình Web", "HTML, CSS, JavaScript và ASP.NET Core MVC"),
            ("Vật lý 12", "Dao động, sóng, điện xoay chiều và lượng tử ánh sáng"),
            ("Hóa học 12", "Este, amin, kim loại và hóa học hữu cơ"),
            ("Tiếng Anh", "Từ vựng, ngữ pháp, đọc hiểu và viết lại câu"),
            ("Ngữ văn", "Đọc hiểu, nghị luận xã hội và nghị luận văn học"),
            ("Sinh học", "Di truyền học, tiến hóa và sinh thái học"),
            ("Lịch sử", "Lịch sử Việt Nam hiện đại và thế giới")
        };

        foreach (var (seed, index) in subjectSeeds.Select((seed, index) => (seed, index)))
        {
            if (!await db.Subjects.AnyAsync(x => x.Name == seed.Item1, cancellationToken))
            {
                db.Subjects.Add(new Subject
                {
                    Name = seed.Item1,
                    Description = seed.Item2,
                    CreatedBy = teachers[index % teachers.Count].Id
                });
            }
        }

        var names = subjectSeeds.Select(x => x.Item1).ToArray();
        return await db.Subjects.Where(x => names.Contains(x.Name)).OrderBy(x => x.Name).ToListAsync(cancellationToken);
    }

    private static async Task EnsureQuestionsAsync(QuizArenaDbContext db, IReadOnlyList<Subject> subjects, IReadOnlyList<User> teachers, CancellationToken cancellationToken)
    {
        foreach (var (subject, subjectIndex) in subjects.Select((subject, index) => (subject, index)))
        {
            var currentCount = await db.Questions.CountAsync(x => x.SubjectId == subject.Id, cancellationToken);
            if (currentCount >= 18) continue;

            var teacherId = teachers[subjectIndex % teachers.Count].Id;
            var needed = 18 - currentCount;
            for (var i = 1; i <= needed; i++)
            {
                var ordinal = currentCount + i;
                db.Questions.Add(BuildSampleQuestion(subject.Id, teacherId, subject.Name, ordinal));
            }
        }
    }

    private static Question BuildSampleQuestion(int subjectId, Guid teacherId, string subjectName, int ordinal)
    {
        var type = ordinal % 6 == 0 ? QuestionType.TextAnswer : ordinal % 4 == 0 ? QuestionType.MultipleChoice : QuestionType.SingleChoice;
        var difficulty = ordinal % 5 == 0 ? Difficulty.Hard : ordinal % 3 == 0 ? Difficulty.Medium : Difficulty.Easy;
        var question = new Question
        {
            SubjectId = subjectId,
            CreatedBy = teacherId,
            Type = type,
            Difficulty = difficulty,
            Points = difficulty == Difficulty.Hard ? 1.5m : 1,
            Content = $"{subjectName} - Câu mẫu {ordinal}: Chọn phương án đúng nhất theo kiến thức đã học."
        };

        if (type == QuestionType.TextAnswer)
        {
            question.Answers.Add(new Answer { Content = (ordinal % 9 + 1).ToString(), IsCorrect = true, OrderIndex = 1 });
            return question;
        }

        question.Answers.Add(new Answer { Content = "Phương án A", IsCorrect = ordinal % 4 == 1, OrderIndex = 1 });
        question.Answers.Add(new Answer { Content = "Phương án B", IsCorrect = ordinal % 4 == 2 || type == QuestionType.MultipleChoice, OrderIndex = 2 });
        question.Answers.Add(new Answer { Content = "Phương án C", IsCorrect = ordinal % 4 == 3, OrderIndex = 3 });
        question.Answers.Add(new Answer { Content = "Phương án D", IsCorrect = ordinal % 4 == 0, OrderIndex = 4 });
        return question;
    }

    private static async Task EnsureExamClassesAsync(QuizArenaDbContext db, CancellationToken cancellationToken)
    {
        var classes = await db.Classes.AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken);
        if (classes.Count == 0) return;

        var primaryClass = classes.FirstOrDefault(x => x.Name == "12A1") ?? classes.First();
        var exams = await db.Exams
            .Where(x => x.IsActive && x.Status != ExamStatus.Draft && x.Status != ExamStatus.Closed)
            .ToListAsync(cancellationToken);

        foreach (var exam in exams)
        {
            var exists = await db.ExamClasses.AsNoTracking()
                .AnyAsync(x => x.ExamId == exam.Id && x.ClassId == primaryClass.Id, cancellationToken);
            if (exists) continue;
            db.ExamClasses.Add(new ExamClass { ExamId = exam.Id, ClassId = primaryClass.Id });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task<List<Exam>> EnsureExamsAsync(QuizArenaDbContext db, IReadOnlyList<Subject> subjects, IReadOnlyList<User> teachers, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        foreach (var (subject, index) in subjects.Take(8).Select((subject, index) => (subject, index)))
        {
            var titles = new[]
            {
                $"{subject.Name} - Kiểm tra 15 phút",
                $"{subject.Name} - Giữa kỳ 2026",
                $"{subject.Name} - Ôn tập cuối kỳ"
            };

            for (var i = 0; i < titles.Length; i++)
            {
                if (await db.Exams.AnyAsync(x => x.Title == titles[i], cancellationToken)) continue;
                db.Exams.Add(new Exam
                {
                    SubjectId = subject.Id,
                    Title = titles[i],
                    Description = i == 0 ? "Bài kiểm tra nhanh theo chương." : i == 1 ? "Đề giữa kỳ có trộn câu hỏi tự động." : "Đề luyện tập chuẩn bị thi cuối kỳ.",
                    DurationMinutes = i == 0 ? 20 : i == 1 ? 60 : 45,
                    GenerateMode = i == 2 ? GenerateMode.ByPoints : GenerateMode.ByCount,
                    QuestionCount = i == 2 ? null : i == 0 ? 8 : 15,
                    TotalPoints = i == 2 ? 10 : null,
                    MaxAttempts = 3,
                    AllowViewAnswer = i != 0,
                    StartTime = now.AddDays(-12 + index * 2 + i * 7),
                    EndTime = now.AddDays(10 + index * 2 + i * 7),
                    CreatedBy = teachers[(index + i) % teachers.Count].Id
                });
            }
        }

        return await db.Exams.Include(x => x.Subject).OrderBy(x => x.StartTime).ToListAsync(cancellationToken);
    }

    private static async Task ResetAttemptsOnOpenExamsAsync(QuizArenaDbContext db, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var openExamIds = await db.Exams.AsNoTracking()
            .Where(x => x.IsActive &&
                        x.Status != ExamStatus.Draft &&
                        x.Status != ExamStatus.Closed &&
                        x.StartTime <= now &&
                        x.EndTime >= now)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        if (openExamIds.Count == 0) return;

        var attempts = await db.ExamAttempts.Where(x => openExamIds.Contains(x.ExamId)).ToListAsync(cancellationToken);
        if (attempts.Count == 0) return;

        db.ExamAttempts.RemoveRange(attempts);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureAttemptsAsync(QuizArenaDbContext db, IReadOnlyList<Exam> exams, IReadOnlyList<User> students, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var historicalExams = exams.Where(x => x.EndTime < now).Take(14).ToList();
        for (var e = 0; e < historicalExams.Count; e++)
        {
            var exam = historicalExams[e];
            var participants = students.Skip(e % 4).Take(14).ToList();
            for (var s = 0; s < participants.Count; s++)
            {
                var student = participants[s];
                if (await db.ExamAttempts.AnyAsync(x => x.ExamId == exam.Id && x.UserId == student.Id, cancellationToken)) continue;

                var submittedAt = DateTime.UtcNow.AddDays(-8 + e).AddMinutes(s * 7);
                var rawScore = 4.8m + ((e * 13 + s * 7) % 52) / 10m;
                var score = Math.Min(10, decimal.Round(rawScore, 1));
                db.ExamAttempts.Add(new ExamAttempt
                {
                    ExamId = exam.Id,
                    UserId = student.Id,
                    StartedAt = submittedAt.AddMinutes(-exam.DurationMinutes),
                    SubmittedAt = submittedAt,
                    Score = score,
                    TotalPoints = 10,
                    Status = s % 13 == 0 ? AttemptStatus.Flagged : AttemptStatus.Submitted,
                    IpAddress = $"192.168.1.{20 + s}",
                    Notes = score < 5 ? "Cần ôn tập lại chương trọng tâm." : null
                });
            }
        }
    }

    private static async Task EnsureMoreNotificationsAsync(QuizArenaDbContext db, Guid creatorId, CancellationToken cancellationToken)
    {
        var seeds = new[]
        {
            ("Mở lịch ôn tập cuối kỳ", "Tổ chuyên môn đã cập nhật các ca ôn tập trước kỳ thi cuối kỳ.", "Kỳ thi"),
            ("Bổ sung câu hỏi vào ngân hàng đề", "Giáo viên đã thêm câu hỏi mới cho các môn trọng tâm.", "Hệ thống"),
            ("Nhắc rà soát điểm bất thường", "Một số bài thi có điểm lệch nhiều so với trung bình lớp.", "Cảnh báo"),
            ("Xuất báo cáo tuần", "Báo cáo học tập tuần này đã sẵn sàng để tải xuống.", "Báo cáo"),
            ("Cập nhật học bạ điện tử", "Hệ thống đã đồng bộ điểm mới nhất vào học bạ điện tử.", "Kết quả")
        };

        for (var i = 0; i < seeds.Length; i++)
        {
            var (title, body, category) = seeds[i];
            if (!await db.Notifications.AnyAsync(x => x.Title == title, cancellationToken))
            {
                db.Notifications.Add(new Notification
                {
                    Title = title,
                    Body = body,
                    Category = category,
                    CreatedBy = creatorId,
                    CreatedAt = DateTime.UtcNow.AddHours(-i - 1)
                });
            }
        }
    }

    private static async Task EnsureMoreExamRoomsAsync(QuizArenaDbContext db, AcademicYear academicYear, IReadOnlyList<Exam> exams, IReadOnlyList<User> teachers, CancellationToken cancellationToken)
    {
        var classes = await db.Classes.Include(x => x.Students).Where(x => x.AcademicYearId == academicYear.Id).OrderBy(x => x.Name).ToListAsync(cancellationToken);
        if (classes.Count == 0) return;

        foreach (var (exam, index) in exams.Take(12).Select((exam, index) => (exam, index)))
        {
            var schoolClass = classes[index % classes.Count];
            if (await db.ExamRooms.AnyAsync(x => x.ExamId == exam.Id && x.ClassId == schoolClass.Id, cancellationToken)) continue;

            var teacher = teachers[index % teachers.Count];
            db.ExamRooms.Add(new ExamRoom
            {
                ExamId = exam.Id,
                ClassId = schoolClass.Id,
                ExamName = exam.Title,
                ClassName = schoolClass.Name,
                Room = schoolClass.Room ?? $"Phòng {200 + index}",
                Shift = $"{exam.StartTime.ToLocalTime():dd/MM HH:mm}",
                ProctorId = teacher.Id,
                ProctorName = teacher.FullName,
                StudentCount = Math.Max(schoolClass.Students.Count(x => x.IsActive), 1),
                Status = exam.StartTime > DateTime.UtcNow ? "Sắp diễn ra" : exam.EndTime < DateTime.UtcNow ? "Đã kết thúc" : "Đang thi"
            });
        }
    }

    private static async Task EnsureMoreBlueprintsAsync(QuizArenaDbContext db, IReadOnlyList<Subject> subjects, Guid teacherId, CancellationToken cancellationToken)
    {
        foreach (var subject in subjects.Take(6))
        {
            if (await db.ExamBlueprintItems.AnyAsync(x => x.SubjectId == subject.Id, cancellationToken)) continue;
            db.ExamBlueprintItems.AddRange(
                new ExamBlueprintItem { SubjectId = subject.Id, Chapter = $"{subject.Name} - Chủ đề nền tảng", EasyCount = 5, MediumCount = 3, HardCount = 0, Points = 3, CreatedBy = teacherId },
                new ExamBlueprintItem { SubjectId = subject.Id, Chapter = $"{subject.Name} - Vận dụng", EasyCount = 2, MediumCount = 5, HardCount = 1, Points = 4, CreatedBy = teacherId },
                new ExamBlueprintItem { SubjectId = subject.Id, Chapter = $"{subject.Name} - Tổng hợp", EasyCount = 1, MediumCount = 2, HardCount = 3, Points = 3, CreatedBy = teacherId });
        }
    }

    private static async Task EnsureMoreQuestionReportsAsync(QuizArenaDbContext db, IReadOnlyList<User> students, CancellationToken cancellationToken)
    {
        var questions = await db.Questions.OrderBy(x => x.Id).Take(12).ToListAsync(cancellationToken);
        var reasons = new[] { "Câu hỏi chưa rõ", "Đáp án cần kiểm tra", "Nội dung có thể nằm ngoài phạm vi ôn tập", "Cần bổ sung giải thích" };
        for (var i = 0; i < questions.Count; i++)
        {
            var question = questions[i];
            if (await db.QuestionReports.AnyAsync(x => x.QuestionId == question.Id && x.Reason == reasons[i % reasons.Length], cancellationToken)) continue;
            db.QuestionReports.Add(new QuestionReport
            {
                QuestionId = question.Id,
                ReportedByUserId = students[i % students.Count].Id,
                Reason = reasons[i % reasons.Length],
                Status = i % 4 == 0 ? "Resolved" : "Pending",
                ReviewedAt = i % 4 == 0 ? DateTime.UtcNow.AddDays(-1) : null
            });
        }
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

    private sealed record SeedUser(string Username, string Email, string FullName);

    private static SeedUser[] SampleTeachers() =>
    [
        new("gv_tranvan", "gv.tranvan@quizarena.vn", "Trần Văn Giáo"),
        new("gv_phamtoan", "gv.phamtoan@quizarena.vn", "Phạm Minh Toàn"),
        new("gv_levatly", "gv.levatly@quizarena.vn", "Lê Thu Vật Lý"),
        new("gv_hoahoc", "gv.hoahoc@quizarena.vn", "Đỗ Thị Hóa"),
        new("gv_nguvan", "gv.nguvan@quizarena.vn", "Nguyễn Mai Văn"),
        new("gv_tienganh", "gv.tienganh@quizarena.vn", "Hoàng Anh Thư")
    ];

    private static SeedUser[] SampleStudents() =>
    [
        new("ts_nguyen01", "ts.nguyen01@student.vn", "Nguyễn Thí Sinh"),
        new("ts_lehoang", "ts.lehoang@student.vn", "Lê Hoàng Sinh"),
        new("hs_anminh", "hs.anminh@student.vn", "Phạm An Minh"),
        new("hs_baongoc", "hs.baongoc@student.vn", "Trần Bảo Ngọc"),
        new("hs_chaugiang", "hs.chaugiang@student.vn", "Lê Châu Giang"),
        new("hs_dangkhoa", "hs.dangkhoa@student.vn", "Nguyễn Đăng Khoa"),
        new("hs_giahuy", "hs.giahuy@student.vn", "Vũ Gia Huy"),
        new("hs_haminh", "hs.haminh@student.vn", "Đỗ Hà Minh"),
        new("hs_khanhlinh", "hs.khanhlinh@student.vn", "Phạm Khánh Linh"),
        new("hs_lamanh", "hs.lamanh@student.vn", "Bùi Lam Anh"),
        new("hs_minhquan", "hs.minhquan@student.vn", "Ngô Minh Quân"),
        new("hs_ngocanh", "hs.ngocanh@student.vn", "Đặng Ngọc Anh"),
        new("hs_phuongnam", "hs.phuongnam@student.vn", "Trịnh Phương Nam"),
        new("hs_quynhchi", "hs.quynhchi@student.vn", "Mai Quỳnh Chi"),
        new("hs_thaovy", "hs.thaovy@student.vn", "Hoàng Thảo Vy"),
        new("hs_tuananh", "hs.tuananh@student.vn", "Đinh Tuấn Anh"),
        new("hs_vietduc", "hs.vietduc@student.vn", "Cao Việt Đức"),
        new("hs_xuanmai", "hs.xuanmai@student.vn", "Lý Xuân Mai"),
        new("hs_yenlinh", "hs.yenlinh@student.vn", "Nguyễn Yến Linh"),
        new("hs_zunhi", "hs.zunhi@student.vn", "Trần Diệu Nhi")
    ];

    private static string[] SampleParents() =>
    [
        "Nguyễn Văn Hùng",
        "Trần Thị Lan",
        "Phạm Quốc Bảo",
        "Lê Thanh Hà",
        "Hoàng Minh Đức",
        "Đỗ Thu Trang"
    ];
}
