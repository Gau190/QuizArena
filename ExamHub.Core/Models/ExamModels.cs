using ExamHub.Core.Entities;

namespace ExamHub.Core.Models;

public record GradeResult(bool IsCorrect, decimal Points);

public record SaveAnswerRequest(Guid AttemptId, int QuestionId, List<int>? AnswerIds, string? TextInput);

public record FlagRequest(Guid AttemptId, string Reason, int Count);

public record QuestionPayload(int Index, int Total, Question Question, ExamAttemptAnswer? CurrentAnswer);
