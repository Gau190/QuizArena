using QuizArena.Core.Entities;

namespace QuizArena.Core.Models;

public record GradeResult(bool IsCorrect, decimal Points);

public record SaveAnswerRequest(Guid AttemptId, int QuestionId, List<int>? AnswerIds, string? TextInput);

public record FlagRequest(Guid AttemptId, string Reason, int Count);

public record ReportQuestionRequest(Guid AttemptId, int QuestionId, string Reason);

public record QuestionPayload(int Index, int Total, Question Question, ExamAttemptAnswer? CurrentAnswer);
