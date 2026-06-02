using ExamHub.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExamHub.Infrastructure.Data;

public class ExamHubDbContext(DbContextOptions<ExamHubDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<Answer> Answers => Set<Answer>();
    public DbSet<Exam> Exams => Set<Exam>();
    public DbSet<ExamAttempt> ExamAttempts => Set<ExamAttempt>();
    public DbSet<ExamAttemptAnswer> ExamAttemptAnswers => Set<ExamAttemptAnswer>();
    public DbSet<ExamQuestionSnapshot> ExamQuestionSnapshots => Set<ExamQuestionSnapshot>();
    public DbSet<ActiveSession> ActiveSessions => Set<ActiveSession>();

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
            e.HasOne(x => x.Creator).WithMany(x => x.SubjectsCreated).HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Question>(e =>
        {
            e.Property(x => x.Content).IsRequired();
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(x => x.Difficulty).HasConversion<string>().HasMaxLength(10).IsRequired();
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
            e.HasOne(x => x.Subject).WithMany(x => x.Exams).HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Creator).WithMany(x => x.ExamsCreated).HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ExamAttempt>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Score).HasPrecision(5, 2);
            e.Property(x => x.TotalPoints).HasPrecision(5, 2);
            e.Property(x => x.IpAddress).HasMaxLength(50);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(x => x.Notes).HasMaxLength(1000);
            e.HasIndex(x => new { x.ExamId, x.UserId }).IsUnique();
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
    }
}
