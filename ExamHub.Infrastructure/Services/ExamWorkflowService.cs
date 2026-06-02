using System.Text.Json;
using ExamHub.Core.Entities;
using ExamHub.Core.Enums;
using ExamHub.Core.Interfaces;
using ExamHub.Core.Models;
using ExamHub.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ExamHub.Infrastructure.Services;

public class ExamWorkflowService(
    ExamHubDbContext db,
    IQuestionDrawService drawService,
    IGradingService gradingService) : IExamWorkflowService
{
    public async Task<Guid> StartAttemptAsync(int examId, Guid userId, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var existing = await db.ExamAttempts
            .FirstOrDefaultAsync(x => x.ExamId == examId && x.UserId == userId, cancellationToken);
        if (existing is not null) return existing.Id;

        var exam = await db.Exams.AsNoTracking()
            .FirstAsync(x => x.Id == examId && x.IsActive && x.StartTime <= DateTime.UtcNow && x.EndTime >= DateTime.UtcNow, cancellationToken);
        var questions = exam.GenerateMode == GenerateMode.ByCount
            ? await drawService.DrawQuestionsAsync(exam.SubjectId, exam.QuestionCount ?? 0, cancellationToken)
            : await drawService.DrawQuestionsByPointsAsync(exam.SubjectId, exam.TotalPoints ?? 0, cancellationToken);

        var attempt = new ExamAttempt { ExamId = exam.Id, UserId = userId, IpAddress = ipAddress };
        byte order = 1;
        foreach (var question in questions)
        {
            attempt.QuestionSnapshots.Add(new ExamQuestionSnapshot { AttemptId = attempt.Id, QuestionId = question.Id, OrderIndex = order++ });
        }

        db.ExamAttempts.Add(attempt);
        await db.SaveChangesAsync(cancellationToken);
        return attempt.Id;
    }

    public async Task<QuestionPayload?> GetQuestionAsync(Guid attemptId, Guid userId, int index, CancellationToken cancellationToken = default)
    {
        var snapshots = await db.ExamQuestionSnapshots.AsNoTracking()
            .Where(x => x.AttemptId == attemptId && x.Attempt!.UserId == userId)
            .OrderBy(x => x.OrderIndex)
            .ToListAsync(cancellationToken);

        if (index < 1 || index > snapshots.Count) return null;

        var snapshot = snapshots[index - 1];
        var question = await db.Questions.AsNoTracking()
            .Include(x => x.Answers.OrderBy(a => a.OrderIndex))
            .FirstAsync(x => x.Id == snapshot.QuestionId, cancellationToken);
        var current = await db.ExamAttemptAnswers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.AttemptId == attemptId && x.QuestionId == question.Id, cancellationToken);

        return new QuestionPayload(index, snapshots.Count, question, current);
    }

    public async Task SaveAnswerAsync(SaveAnswerRequest request, Guid userId, CancellationToken cancellationToken = default)
    {
        var ownsAttempt = await db.ExamAttempts.AnyAsync(x => x.Id == request.AttemptId && x.UserId == userId && x.Status == AttemptStatus.InProgress, cancellationToken);
        if (!ownsAttempt) throw new InvalidOperationException("Attempt is not active.");

        var answer = await db.ExamAttemptAnswers.FirstOrDefaultAsync(x => x.AttemptId == request.AttemptId && x.QuestionId == request.QuestionId, cancellationToken);
        if (answer is null)
        {
            answer = new ExamAttemptAnswer { AttemptId = request.AttemptId, QuestionId = request.QuestionId };
            db.ExamAttemptAnswers.Add(answer);
        }

        answer.AnswerIds = request.AnswerIds is null ? null : JsonSerializer.Serialize(request.AnswerIds.Order());
        answer.TextInput = request.TextInput;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SubmitAsync(Guid attemptId, Guid userId, bool timedOut = false, CancellationToken cancellationToken = default)
    {
        var attempt = await db.ExamAttempts
            .Include(x => x.QuestionSnapshots.OrderBy(s => s.OrderIndex))
            .Include(x => x.Answers)
            .FirstOrDefaultAsync(x => x.Id == attemptId && x.UserId == userId, cancellationToken);
        if (attempt is null || attempt.Status is not AttemptStatus.InProgress and not AttemptStatus.Flagged) return;

        var wasFlagged = attempt.Status == AttemptStatus.Flagged;
        var questionIds = attempt.QuestionSnapshots.Select(x => x.QuestionId).ToList();
        var questions = await db.Questions.Include(x => x.Answers)
            .Where(x => questionIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var total = questions.Values.Sum(x => x.Points);
        var score = 0m;

        foreach (var questionId in questionIds)
        {
            var answer = attempt.Answers.FirstOrDefault(x => x.QuestionId == questionId);
            if (answer is null)
            {
                answer = new ExamAttemptAnswer { AttemptId = attempt.Id, QuestionId = questionId, AnswerIds = "[]" };
                db.ExamAttemptAnswers.Add(answer);
            }

            var result = gradingService.GradeAnswer(questions[questionId], answer);
            answer.IsCorrect = result.IsCorrect;
            answer.PointsEarned = result.Points;
            score += result.Points;
        }

        attempt.Score = score;
        attempt.TotalPoints = total;
        attempt.SubmittedAt = DateTime.UtcNow;
        attempt.Status = wasFlagged ? AttemptStatus.Flagged : timedOut ? AttemptStatus.TimedOut : AttemptStatus.Submitted;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<(int Remaining, bool AutoSubmitted)> GetTimeRemainingAsync(Guid attemptId, Guid userId, CancellationToken cancellationToken = default)
    {
        var attempt = await db.ExamAttempts.AsNoTracking()
            .Include(x => x.Exam)
            .FirstOrDefaultAsync(x => x.Id == attemptId && x.UserId == userId, cancellationToken);

        if (attempt is null || attempt.Exam is null || attempt.Status != AttemptStatus.InProgress) return (0, false);

        var deadline = attempt.StartedAt.AddMinutes(attempt.Exam.DurationMinutes);
        var remaining = (int)(deadline - DateTime.UtcNow).TotalSeconds;
        if (remaining > 0) return (remaining, false);

        await SubmitAsync(attemptId, userId, timedOut: true, cancellationToken);
        return (0, true);
    }

    public async Task FlagAttemptAsync(FlagRequest request, Guid userId, CancellationToken cancellationToken = default)
    {
        var attempt = await db.ExamAttempts.FirstOrDefaultAsync(x => x.Id == request.AttemptId && x.UserId == userId, cancellationToken);
        if (attempt is null) return;

        attempt.Notes = $"{attempt.Notes}\n{DateTime.UtcNow:O} {request.Reason} #{request.Count}".Trim();
        if (request.Reason == "tab_switch" && request.Count >= 3 && attempt.Status == AttemptStatus.InProgress)
        {
            attempt.Status = AttemptStatus.Flagged;
            await db.SaveChangesAsync(cancellationToken);
            await SubmitAsync(request.AttemptId, userId, cancellationToken: cancellationToken);
            return;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
