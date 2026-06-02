using ExamHub.Core.Enums;

namespace ExamHub.Core.Entities;

public class ExamAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int ExamId { get; set; }
    public Guid UserId { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SubmittedAt { get; set; }
    public decimal? Score { get; set; }
    public decimal? TotalPoints { get; set; }
    public string? IpAddress { get; set; }
    public AttemptStatus Status { get; set; } = AttemptStatus.InProgress;
    public string? Notes { get; set; }

    public Exam? Exam { get; set; }
    public User? User { get; set; }
    public ICollection<ExamAttemptAnswer> Answers { get; set; } = new List<ExamAttemptAnswer>();
    public ICollection<ExamQuestionSnapshot> QuestionSnapshots { get; set; } = new List<ExamQuestionSnapshot>();
}
