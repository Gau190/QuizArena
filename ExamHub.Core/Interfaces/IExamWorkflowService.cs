using ExamHub.Core.Models;

namespace ExamHub.Core.Interfaces;

public interface IExamWorkflowService
{
    Task<Guid> StartAttemptAsync(int examId, Guid userId, string? ipAddress, CancellationToken cancellationToken = default);
    Task<QuestionPayload?> GetQuestionAsync(Guid attemptId, Guid userId, int index, CancellationToken cancellationToken = default);
    Task SaveAnswerAsync(SaveAnswerRequest request, Guid userId, CancellationToken cancellationToken = default);
    Task SubmitAsync(Guid attemptId, Guid userId, bool timedOut = false, CancellationToken cancellationToken = default);
    Task<(int Remaining, bool AutoSubmitted)> GetTimeRemainingAsync(Guid attemptId, Guid userId, CancellationToken cancellationToken = default);
    Task FlagAttemptAsync(FlagRequest request, Guid userId, CancellationToken cancellationToken = default);
}
