using QuizArena.Core.Entities;
using QuizArena.Core.Interfaces;
using QuizArena.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace QuizArena.Infrastructure.Services;

public class AcademicRecordService(QuizArenaDbContext db) : IAcademicRecordService
{
    public async Task RecalculateAsync(Guid studentId, int subjectId, DateTime submittedAt, CancellationToken cancellationToken = default)
    {
        var semester = await ResolveSemesterAsync(submittedAt, cancellationToken);
        if (semester is null) return;

        var startUtc = semester.StartDate.ToDateTime(TimeOnly.MinValue).ToUniversalTime();
        var endUtc = semester.EndDate.ToDateTime(TimeOnly.MaxValue).ToUniversalTime();

        var attempts = await db.ExamAttempts.AsNoTracking()
            .Include(x => x.Exam)
            .Where(x => x.UserId == studentId &&
                        x.Exam!.SubjectId == subjectId &&
                        x.Score.HasValue &&
                        x.SubmittedAt >= startUtc &&
                        x.SubmittedAt <= endUtc)
            .ToListAsync(cancellationToken);

        if (attempts.Count == 0) return;

        var average = decimal.Round(attempts.Average(x => x.Score ?? 0), 1);
        var best = attempts.Max(x => x.Score ?? 0);
        var lastDate = attempts.Max(x => x.SubmittedAt);

        var record = await db.AcademicRecords.FirstOrDefaultAsync(
            x => x.StudentId == studentId && x.SubjectId == subjectId && x.SemesterId == semester.Id,
            cancellationToken);

        if (record is null)
        {
            record = new AcademicRecord { StudentId = studentId, SubjectId = subjectId, SemesterId = semester.Id };
            db.AcademicRecords.Add(record);
        }

        record.AverageScore = average;
        record.BestScore = best;
        record.TotalAttempts = attempts.Count;
        record.LastExamDate = lastDate;
        record.Rank = RankScore(average);
        record.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Semester?> ResolveSemesterAsync(DateTime submittedAt, CancellationToken cancellationToken)
    {
        var local = submittedAt.ToLocalTime();
        var month = local.Month;
        var semesters = await db.Semesters.AsNoTracking().OrderBy(x => x.Id).ToListAsync(cancellationToken);
        if (semesters.Count == 0) return null;

        var hk1 = semesters.FirstOrDefault(x => x.Name.Contains('1', StringComparison.OrdinalIgnoreCase)) ?? semesters.First();
        var hk2 = semesters.FirstOrDefault(x => x.Name.Contains('2', StringComparison.OrdinalIgnoreCase)) ?? semesters.Last();
        return month is >= 8 and <= 12 ? hk1 : hk2;
    }

    private static string RankScore(decimal score)
    {
        if (score >= 9) return "Xuất sắc";
        if (score >= 8) return "Giỏi";
        if (score >= 6.5m) return "Khá";
        if (score >= 5) return "Trung bình";
        return "Yếu";
    }
}