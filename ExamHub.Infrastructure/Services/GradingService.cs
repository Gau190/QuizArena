using System.Text.Json;
using System.Text.RegularExpressions;
using ExamHub.Core.Entities;
using ExamHub.Core.Enums;
using ExamHub.Core.Interfaces;
using ExamHub.Core.Models;

namespace ExamHub.Infrastructure.Services;

public class GradingService : IGradingService
{
    public GradeResult GradeAnswer(Question question, ExamAttemptAnswer studentAnswer)
    {
        return question.Type switch
        {
            QuestionType.SingleChoice => GradeChoice(question, studentAnswer),
            QuestionType.MultipleChoice => GradeChoice(question, studentAnswer),
            QuestionType.TextAnswer => GradeText(question, studentAnswer),
            _ => new GradeResult(false, 0)
        };
    }

    private static GradeResult GradeChoice(Question question, ExamAttemptAnswer answer)
    {
        var correctIds = question.Answers.Where(x => x.IsCorrect).Select(x => x.Id).Order().ToList();
        var selectedIds = JsonSerializer.Deserialize<List<int>>(answer.AnswerIds ?? "[]")?.Order().ToList() ?? [];
        var isCorrect = correctIds.SequenceEqual(selectedIds);
        return new GradeResult(isCorrect, isCorrect ? question.Points : 0);
    }

    private static GradeResult GradeText(Question question, ExamAttemptAnswer answer)
    {
        static string Normalize(string value) => Regex.Replace(value.Trim().ToLowerInvariant(), "\\s+", " ");
        var studentNorm = Normalize(answer.TextInput ?? string.Empty);
        var correct = question.Answers.Where(x => x.IsCorrect).Select(x => Normalize(x.Content));
        var isCorrect = correct.Any(x => x == studentNorm);
        return new GradeResult(isCorrect, isCorrect ? question.Points : 0);
    }
}
