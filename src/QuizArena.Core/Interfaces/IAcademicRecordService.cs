namespace QuizArena.Core.Interfaces;

public interface IAcademicRecordService
{
    Task RecalculateAsync(Guid studentId, int subjectId, DateTime submittedAt, CancellationToken cancellationToken = default);
}