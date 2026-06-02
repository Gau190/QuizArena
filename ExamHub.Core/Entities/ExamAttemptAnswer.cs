namespace ExamHub.Core.Entities;

public class ExamAttemptAnswer
{
    public int Id { get; set; }
    public Guid AttemptId { get; set; }
    public int QuestionId { get; set; }
    public string? AnswerIds { get; set; }
    public string? TextInput { get; set; }
    public bool? IsCorrect { get; set; }
    public decimal? PointsEarned { get; set; }

    public ExamAttempt? Attempt { get; set; }
    public Question? Question { get; set; }
}
