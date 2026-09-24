namespace QuizArena.Core.Entities;

public class QuestionReport
{
    public int Id { get; set; }
    public int QuestionId { get; set; }
    public Guid? ReportedByUserId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public string? TeacherNote { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }

    public Question? Question { get; set; }
    public User? Reporter { get; set; }
}
