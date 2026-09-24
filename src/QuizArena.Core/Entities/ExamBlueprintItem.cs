namespace QuizArena.Core.Entities;

public class ExamBlueprintItem
{
    public int Id { get; set; }
    public int? SubjectId { get; set; }
    public string Chapter { get; set; } = string.Empty;
    public int EasyCount { get; set; }
    public int MediumCount { get; set; }
    public int HardCount { get; set; }
    public decimal Points { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Subject? Subject { get; set; }
    public User? Creator { get; set; }
}
