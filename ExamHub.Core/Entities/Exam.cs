using ExamHub.Core.Enums;

namespace ExamHub.Core.Entities;

public class Exam
{
    public int Id { get; set; }
    public int SubjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DurationMinutes { get; set; }
    public GenerateMode GenerateMode { get; set; }
    public int? QuestionCount { get; set; }
    public decimal? TotalPoints { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Subject? Subject { get; set; }
    public User? Creator { get; set; }
    public ICollection<ExamAttempt> Attempts { get; set; } = new List<ExamAttempt>();
}
