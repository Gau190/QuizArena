using QuizArena.Core.Enums;

namespace QuizArena.Core.Entities;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }

    public ICollection<Subject> SubjectsCreated { get; set; } = new List<Subject>();
    public ICollection<Question> QuestionsCreated { get; set; } = new List<Question>();
    public ICollection<Exam> ExamsCreated { get; set; } = new List<Exam>();
    public ICollection<ExamAttempt> Attempts { get; set; } = new List<ExamAttempt>();
}
