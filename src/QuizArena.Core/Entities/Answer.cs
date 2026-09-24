namespace QuizArena.Core.Entities;

public class Answer
{
    public int Id { get; set; }
    public int QuestionId { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public byte OrderIndex { get; set; }

    public Question? Question { get; set; }
}
