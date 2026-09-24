using System.Text.Json;
using QuizArena.Core.Entities;
using QuizArena.Core.Enums;
using QuizArena.Infrastructure.Services;

namespace QuizArena.Tests;

public class GradingServiceTests
{
    private readonly GradingService _service = new();

    [Fact]
    public void SingleChoice_RequiresCorrectAnswer()
    {
        var question = new Question
        {
            Type = QuestionType.SingleChoice,
            Points = 1,
            Answers =
            {
                new Answer { Id = 1, Content = "Sai" },
                new Answer { Id = 2, Content = "Đúng", IsCorrect = true }
            }
        };
        var answer = new ExamAttemptAnswer { AnswerIds = JsonSerializer.Serialize(new[] { 2 }) };

        var result = _service.GradeAnswer(question, answer);

        Assert.True(result.IsCorrect);
        Assert.Equal(1, result.Points);
    }

    [Fact]
    public void MultipleChoice_RequiresExactSet()
    {
        var question = new Question
        {
            Type = QuestionType.MultipleChoice,
            Points = 1.5m,
            Answers =
            {
                new Answer { Id = 1, IsCorrect = true },
                new Answer { Id = 2 },
                new Answer { Id = 3, IsCorrect = true }
            }
        };
        var answer = new ExamAttemptAnswer { AnswerIds = JsonSerializer.Serialize(new[] { 3, 1 }) };

        var result = _service.GradeAnswer(question, answer);

        Assert.True(result.IsCorrect);
        Assert.Equal(1.5m, result.Points);
    }

    [Fact]
    public void TextAnswer_NormalizesCaseAndSpaces()
    {
        var question = new Question
        {
            Type = QuestionType.TextAnswer,
            Points = 1,
            Answers = { new Answer { Content = "ma trận đơn vị", IsCorrect = true } }
        };
        var answer = new ExamAttemptAnswer { TextInput = "  MA   TRẬN  ĐƠN VỊ " };

        var result = _service.GradeAnswer(question, answer);

        Assert.True(result.IsCorrect);
        Assert.Equal(1, result.Points);
    }
}
