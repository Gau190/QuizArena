using QuizArena.Core.Enums;

namespace QuizArena.Core.Entities;

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
    public int EasyCount { get; set; }
    public int MediumCount { get; set; }
    public int HardCount { get; set; }
    public int MaxAttempts { get; set; } = 1;
    public decimal PassScore { get; set; } = 5m;
    public bool ShuffleQuestions { get; set; } = true;
    public bool ShuffleAnswers { get; set; } = true;
    public bool AntiCheat { get; set; } = true;
    public bool AllowViewAnswer { get; set; }
    public bool AutoSubmit { get; set; } = true;
    public ExamStatus Status { get; set; } = ExamStatus.Published;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Subject? Subject { get; set; }
    public User? Creator { get; set; }
    public ICollection<ExamAttempt> Attempts { get; set; } = new List<ExamAttempt>();
    public ICollection<ExamClass> Classes { get; set; } = new List<ExamClass>();
}
