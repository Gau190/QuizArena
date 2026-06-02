using ExamHub.Core.Entities;

namespace ExamHub.Core.Interfaces;

public interface IQuestionDrawService
{
    Task<List<Question>> DrawQuestionsAsync(int subjectId, int count, CancellationToken cancellationToken = default);
    Task<List<Question>> DrawQuestionsByPointsAsync(int subjectId, decimal targetPoints, CancellationToken cancellationToken = default);
}
