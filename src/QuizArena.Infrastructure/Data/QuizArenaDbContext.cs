using QuizArena.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace QuizArena.Infrastructure.Data;

public class QuizArenaDbContext(DbContextOptions<QuizArenaDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<Answer> Answers => Set<Answer>();
    public DbSet<Exam> Exams => Set<Exam>();
    public DbSet<ExamClass> ExamClasses => Set<ExamClass>();
    public DbSet<ExamAttempt> ExamAttempts => Set<ExamAttempt>();
    public DbSet<ExamAttemptAnswer> ExamAttemptAnswers => Set<ExamAttemptAnswer>();
    public DbSet<ExamQuestionSnapshot> ExamQuestionSnapshots => Set<ExamQuestionSnapshot>();
    public DbSet<ActiveSession> ActiveSessions => Set<ActiveSession>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<AcademicYear> AcademicYears => Set<AcademicYear>();
    public DbSet<Semester> Semesters => Set<Semester>();
    public DbSet<SchoolClass> Classes => Set<SchoolClass>();
    public DbSet<ClassStudent> ClassStudents => Set<ClassStudent>();
    public DbSet<StudentProfile> StudentProfiles => Set<StudentProfile>();
    public DbSet<TeacherProfile> TeacherProfiles => Set<TeacherProfile>();
    public DbSet<ExamRoom> ExamRooms => Set<ExamRoom>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<QuestionReport> QuestionReports => Set<QuestionReport>();
    public DbSet<ExamBlueprintItem> ExamBlueprintItems => Set<ExamBlueprintItem>();
    public DbSet<ExamBlueprintConfig> ExamBlueprintConfigs => Set<ExamBlueprintConfig>();
    public DbSet<AcademicRecord> AcademicRecords => Set<AcademicRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Username).HasMaxLength(50).IsRequired();
            e.Property(x => x.Email).HasMaxLength(150).IsRequired();
            e.Property(x => x.FullName).HasMaxLength(100).IsRequired();
            e.Property(x => x.PasswordHash).HasMaxLength(255).IsRequired();
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.HasIndex(x => x.Username).IsUnique();
            e.HasIndex(x => x.Email).IsUnique();
        });

        modelBuilder.Entity<Subject>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Description).HasMaxLength(500);
            e.Property(x => x.Code).HasMaxLength(50);
            e.Property(x => x.ColorHex).HasMaxLength(7);
            e.HasOne(x => x.Creator).WithMany(x => x.SubjectsCreated).HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Question>(e =>
        {
            e.Property(x => x.Content).IsRequired();
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(x => x.Difficulty).HasConversion<string>().HasMaxLength(10).IsRequired();
            e.Property(x => x.Tags).HasMaxLength(500);
            e.Property(x => x.Chapter).HasMaxLength(200);
            e.Property(x => x.Explanation).HasMaxLength(2000);
            e.Property(x => x.Points).HasPrecision(4, 2);
            e.HasOne(x => x.Subject).WithMany(x => x.Questions).HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Creator).WithMany(x => x.QuestionsCreated).HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Answer>(e =>
        {
            e.Property(x => x.Content).IsRequired();
            e.HasOne(x => x.Question).WithMany(x => x.Answers).HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Exam>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasMaxLength(500);
            e.Property(x => x.GenerateMode).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(x => x.TotalPoints).HasPrecision(6, 2);
            e.Property(x => x.PassScore).HasPrecision(5, 2);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.HasOne(x => x.Subject).WithMany(x => x.Exams).HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Creator).WithMany(x => x.ExamsCreated).HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ExamClass>(e =>
        {
            e.HasKey(x => new { x.ExamId, x.ClassId });
            e.HasOne(x => x.Exam).WithMany(x => x.Classes).HasForeignKey(x => x.ExamId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Class).WithMany().HasForeignKey(x => x.ClassId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ExamAttempt>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Score).HasPrecision(5, 2);
            e.Property(x => x.TotalPoints).HasPrecision(5, 2);
            e.Property(x => x.IpAddress).HasMaxLength(50);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(x => x.Notes).HasMaxLength(1000);
            e.HasIndex(x => new { x.ExamId, x.UserId });
            e.HasOne(x => x.Exam).WithMany(x => x.Attempts).HasForeignKey(x => x.ExamId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.User).WithMany(x => x.Attempts).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ExamAttemptAnswer>(e =>
        {
            e.Property(x => x.TextInput).HasMaxLength(500);
            e.Property(x => x.PointsEarned).HasPrecision(4, 2);
            e.HasIndex(x => new { x.AttemptId, x.QuestionId }).IsUnique();
            e.HasOne(x => x.Attempt).WithMany(x => x.Answers).HasForeignKey(x => x.AttemptId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Question).WithMany().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ExamQuestionSnapshot>(e =>
        {
            e.HasKey(x => new { x.AttemptId, x.QuestionId });
            e.HasOne(x => x.Attempt).WithMany(x => x.QuestionSnapshots).HasForeignKey(x => x.AttemptId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Question).WithMany().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ActiveSession>(e =>
        {
            e.HasKey(x => x.UserId);
            e.Property(x => x.TokenHash).HasMaxLength(255).IsRequired();
            e.HasOne(x => x.User).WithOne().HasForeignKey<ActiveSession>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Organization>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Code).HasMaxLength(50).IsRequired();
            e.Property(x => x.Address).HasMaxLength(500);
            e.Property(x => x.Phone).HasMaxLength(20);
            e.Property(x => x.Email).HasMaxLength(200);
            e.Property(x => x.Website).HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(1000);
            e.Property(x => x.LogoUrl).HasMaxLength(500);
            e.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<AcademicYear>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(50).IsRequired();
            e.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.OrganizationId, x.Name }).IsUnique();
        });

        modelBuilder.Entity<Semester>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(50).IsRequired();
            e.HasOne(x => x.AcademicYear).WithMany(x => x.Semesters).HasForeignKey(x => x.AcademicYearId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.AcademicYearId, x.Name }).IsUnique();
        });

        modelBuilder.Entity<SchoolClass>(e =>
        {
            e.ToTable("Classes");
            e.Property(x => x.Name).HasMaxLength(50).IsRequired();
            e.Property(x => x.Grade).HasMaxLength(10);
            e.Property(x => x.Track).HasMaxLength(100);
            e.Property(x => x.Room).HasMaxLength(50);
            e.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.AcademicYear).WithMany(x => x.Classes).HasForeignKey(x => x.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.HomeTeacher).WithMany().HasForeignKey(x => x.HomeTeacherId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => new { x.AcademicYearId, x.Name }).IsUnique();
        });

        modelBuilder.Entity<ClassStudent>(e =>
        {
            e.HasKey(x => new { x.ClassId, x.StudentId });
            e.HasOne(x => x.Class).WithMany(x => x.Students).HasForeignKey(x => x.ClassId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StudentProfile>(e =>
        {
            e.HasKey(x => x.UserId);
            e.Property(x => x.StudentCode).HasMaxLength(50).IsRequired();
            e.Property(x => x.Gender).HasMaxLength(10);
            e.Property(x => x.ParentName).HasMaxLength(200);
            e.Property(x => x.ParentPhone).HasMaxLength(20);
            e.Property(x => x.ParentEmail).HasMaxLength(200);
            e.Property(x => x.Address).HasMaxLength(500);
            e.Property(x => x.Conduct).HasMaxLength(20);
            e.Property(x => x.AcademicLevel).HasMaxLength(20);
            e.Property(x => x.Notes).HasMaxLength(2000);
            e.HasIndex(x => x.StudentCode).IsUnique();
            e.HasOne(x => x.User).WithOne().HasForeignKey<StudentProfile>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TeacherProfile>(e =>
        {
            e.HasKey(x => x.UserId);
            e.Property(x => x.EmployeeCode).HasMaxLength(50).IsRequired();
            e.Property(x => x.Specialization).HasMaxLength(200);
            e.Property(x => x.Degree).HasMaxLength(100);
            e.HasIndex(x => x.EmployeeCode).IsUnique();
            e.HasOne(x => x.User).WithOne().HasForeignKey<TeacherProfile>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ExamRoom>(e =>
        {
            e.Property(x => x.ExamName).HasMaxLength(200).IsRequired();
            e.Property(x => x.ClassName).HasMaxLength(50).IsRequired();
            e.Property(x => x.Room).HasMaxLength(50).IsRequired();
            e.Property(x => x.Shift).HasMaxLength(100).IsRequired();
            e.Property(x => x.ProctorName).HasMaxLength(200);
            e.Property(x => x.Status).HasMaxLength(30).IsRequired();
            e.HasOne(x => x.Exam).WithMany().HasForeignKey(x => x.ExamId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Class).WithMany(x => x.ExamRooms).HasForeignKey(x => x.ClassId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Proctor).WithMany().HasForeignKey(x => x.ProctorId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Notification>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Body).HasMaxLength(2000).IsRequired();
            e.Property(x => x.Category).HasMaxLength(50).IsRequired();
            e.Property(x => x.RecipientRole).HasMaxLength(20);
            e.HasOne(x => x.Creator).WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<QuestionReport>(e =>
        {
            e.Property(x => x.Reason).HasMaxLength(500).IsRequired();
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.Property(x => x.TeacherNote).HasMaxLength(1000);
            e.HasOne(x => x.Question).WithMany().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Reporter).WithMany().HasForeignKey(x => x.ReportedByUserId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ExamBlueprintItem>(e =>
        {
            e.Property(x => x.Chapter).HasMaxLength(200).IsRequired();
            e.Property(x => x.Points).HasPrecision(6, 2);
            e.HasOne(x => x.Subject).WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Creator).WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ExamBlueprintConfig>(e =>
        {
            e.HasKey(x => new { x.UserId, x.SubjectId });
            e.Property(x => x.TargetTotalPoints).HasPrecision(6, 2);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Subject).WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AcademicRecord>(e =>
        {
            e.Property(x => x.AverageScore).HasPrecision(5, 2);
            e.Property(x => x.BestScore).HasPrecision(5, 2);
            e.Property(x => x.Rank).HasMaxLength(20);
            e.Property(x => x.TeacherComment).HasMaxLength(2000);
            e.HasIndex(x => new { x.StudentId, x.SubjectId, x.SemesterId }).IsUnique();
            e.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Subject).WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Semester).WithMany().HasForeignKey(x => x.SemesterId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
