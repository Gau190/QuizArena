namespace QuizArena.Core.Entities;

public class Organization
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? Description { get; set; }
    public string? LogoUrl { get; set; }
    public bool AllowViewAnswerAfterSubmit { get; set; }
    public bool AutoSubmitOnTimeout { get; set; } = true;
    public bool AntiCheatTabSwitch { get; set; } = true;
    public bool ShuffleQuestions { get; set; } = true;
    public bool ShuffleAnswers { get; set; } = true;
    public bool ShareQuestionBank { get; set; } = true;
    public bool SendEmailOnResult { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
