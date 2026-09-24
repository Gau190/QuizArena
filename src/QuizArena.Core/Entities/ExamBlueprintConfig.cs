using QuizArena.Core.Enums;

namespace QuizArena.Core.Entities;

public class ExamBlueprintConfig
{
    public Guid UserId { get; set; }
    public int SubjectId { get; set; }
    public GenerateMode GenerateMode { get; set; } = GenerateMode.ByCount;
    public decimal TargetTotalPoints { get; set; } = 10m;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }
    public Subject? Subject { get; set; }
}