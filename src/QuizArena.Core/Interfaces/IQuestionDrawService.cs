using QuizArena.Core.Entities;
using QuizArena.Core.Enums;

namespace QuizArena.Core.Interfaces;

public interface IQuestionDrawService
{
    Task<List<Question>> DrawQuestionsAsync(int subjectId, int count, CancellationToken cancellationToken = default);
    Task<List<Question>> DrawQuestionsByDifficultyAsync(int subjectId, int easyCount, int mediumCount, int hardCount, CancellationToken cancellationToken = default);
    Task<List<Question>> DrawQuestionsByPointsAsync(int subjectId, decimal targetPoints, CancellationToken cancellationToken = default);
}
