using ExamHub.Core.Enums;

namespace ExamHub.Core.Entities;

public class Question
{
    public int Id { get; set; }
    public int SubjectId { get; set; }
    public string Content { get; set; } = string.Empty;
    public QuestionType Type { get; set; }
    public Difficulty Difficulty { get; set; }
    public decimal Points { get; set; } = 1.0m;
    public bool IsActive { get; set; } = true;
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Subject? Subject { get; set; }
    public User? Creator { get; set; }
    public ICollection<Answer> Answers { get; set; } = new List<Answer>();
}
