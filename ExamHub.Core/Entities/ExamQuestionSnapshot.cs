namespace ExamHub.Core.Entities;

public class ExamQuestionSnapshot
{
    public Guid AttemptId { get; set; }
    public int QuestionId { get; set; }
    public byte OrderIndex { get; set; }

    public ExamAttempt? Attempt { get; set; }
    public Question? Question { get; set; }
}
