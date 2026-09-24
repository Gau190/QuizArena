using QuizArena.Core.Enums;

namespace QuizArena.Core.Entities;

public class Question
{
    public int Id { get; set; }
    public int SubjectId { get; set; }
    public string Content { get; set; } = string.Empty;
    public QuestionType Type { get; set; }
    public Difficulty Difficulty { get; set; }
    public decimal Points { get; set; } = 1.0m;
    public bool IsActive { get; set; } = true;
    public bool IsApproved { get; set; } = true;
    public bool IsShared { get; set; } = true;
    public string? Tags { get; set; }
    public string? Chapter { get; set; }
    public string? Explanation { get; set; }
    public int TimesUsed { get; set; }
    public double CorrectRate { get; set; }
    public int ReportCount { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Subject? Subject { get; set; }
    public User? Creator { get; set; }
    public ICollection<Answer> Answers { get; set; } = new List<Answer>();
}
