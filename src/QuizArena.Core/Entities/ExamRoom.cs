namespace QuizArena.Core.Entities;

public class ExamRoom
{
    public int Id { get; set; }
    public int? ExamId { get; set; }
    public int? ClassId { get; set; }
    public string ExamName { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public string Room { get; set; } = string.Empty;
    public string Shift { get; set; } = string.Empty;
    public Guid? ProctorId { get; set; }
    public string? ProctorName { get; set; }
    public int StudentCount { get; set; }
    public string Status { get; set; } = "Sắp diễn ra";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Exam? Exam { get; set; }
    public SchoolClass? Class { get; set; }
    public User? Proctor { get; set; }
}
