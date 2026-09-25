using System.Collections.Concurrent;
using System.Text.Json;
using QuizArena.Core.Entities;
using QuizArena.Core.Enums;
using QuizArena.Core.Interfaces;
using QuizArena.Core.Models;
using QuizArena.Infrastructure.Data;
using QuizArena.Infrastructure.Utilities;
using Microsoft.EntityFrameworkCore;

namespace QuizArena.Infrastructure.Services;

public class ExamWorkflowService(
    QuizArenaDbContext db,
    IQuestionDrawService drawService,
    IGradingService gradingService,
    IAcademicRecordService academicRecordService) : IExamWorkflowService
{
    // Network latency allowance when comparing client actions against the server-side deadline.
    private static readonly TimeSpan AnswerGrace = TimeSpan.FromSeconds(15);

    // Khoá theo khoá nghiệp vụ để hai yêu cầu đồng thời (bắt đầu thi, nộp bài) không cùng đi qua bước kiểm tra.
    // Có hiệu lực trong một tiến trình; nếu chạy nhiều máy chủ cần thêm ràng buộc duy nhất ở CSDL.
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new();
    private static SemaphoreSlim GateFor(string key) => Gates.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

    public async Task<Guid> StartAttemptAsync(int examId, Guid userId, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var gate = GateFor($"start:{userId}:{examId}");
        await gate.WaitAsync(cancellationToken);
        try { return await StartAttemptCoreAsync(examId, userId, ipAddress, cancellationToken); }
        finally { gate.Release(); }
    }

    private async Task<Guid> StartAttemptCoreAsync(int examId, Guid userId, string? ipAddress, CancellationToken cancellationToken)
    {
        var exam = await db.Exams.AsNoTracking()
            .Include(x => x.Classes)
            .FirstOrDefaultAsync(x => x.Id == examId &&
                                      x.IsActive &&
                                      x.Status != ExamStatus.Draft &&
                                      x.Status != ExamStatus.Closed &&
                                      x.StartTime <= DateTime.UtcNow &&
                                      x.EndTime >= DateTime.UtcNow,
                cancellationToken);
        if (exam is null)
        {
            throw new InvalidOperationException("Kỳ thi không khả dụng hoặc đã hết hạn.");
        }

        var examClassIds = exam.Classes.Select(x => x.ClassId).ToList();
        if (examClassIds.Count > 0)
        {
            var inClass = await db.ClassStudents.AnyAsync(
                x => x.StudentId == userId && x.IsActive && examClassIds.Contains(x.ClassId),
                cancellationToken);
            if (!inClass)
            {
                throw new InvalidOperationException("Bạn không thuộc lớp được phép tham gia kỳ thi này.");
            }
        }

        var inProgress = await db.ExamAttempts
            .FirstOrDefaultAsync(x => x.ExamId == examId && x.UserId == userId && x.Status == AttemptStatus.InProgress, cancellationToken);
        if (inProgress is not null) return inProgress.Id;

        var completedCount = await db.ExamAttempts.CountAsync(
            x => x.ExamId == examId && x.UserId == userId && x.Status != AttemptStatus.InProgress,
            cancellationToken);
        if (completedCount >= exam.MaxAttempts)
        {
            throw new InvalidOperationException($"Bạn đã hết lượt thi (tối đa {exam.MaxAttempts} lần).");
        }
        var drawn = exam.GenerateMode == GenerateMode.ByCount
            ? exam.EasyCount + exam.MediumCount + exam.HardCount > 0
                ? await drawService.DrawQuestionsByDifficultyAsync(exam.SubjectId, exam.EasyCount, exam.MediumCount, exam.HardCount, cancellationToken)
                : await drawService.DrawQuestionsAsync(exam.SubjectId, exam.QuestionCount ?? 0, cancellationToken)
            : await drawService.DrawQuestionsByPointsAsync(exam.SubjectId, exam.TotalPoints ?? 0, cancellationToken);

        var questions = drawn.ToList();
        if (exam.ShuffleQuestions && questions.Count > 1)
        {
            ShuffleHelper.Shuffle(questions, HashCode.Combine(exam.Id, userId));
        }

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
        var attempt = await db.ExamAttempts.AsNoTracking()
            .Include(x => x.Exam)
            .FirstOrDefaultAsync(x => x.Id == attemptId && x.UserId == userId, cancellationToken);
        if (attempt?.Exam is null) return null;

        var snapshots = await db.ExamQuestionSnapshots.AsNoTracking()
            .Where(x => x.AttemptId == attemptId)
            .OrderBy(x => x.OrderIndex)
            .ToListAsync(cancellationToken);

        if (index < 1 || index > snapshots.Count) return null;

        var snapshot = snapshots[index - 1];
        var question = await db.Questions.AsNoTracking()
            .Include(x => x.Answers.OrderBy(a => a.OrderIndex))
            .FirstAsync(x => x.Id == snapshot.QuestionId, cancellationToken);

        if (attempt.Exam.ShuffleAnswers && question.Type != QuestionType.TextAnswer && question.Answers.Count > 1)
        {
            var shuffled = question.Answers.ToList();
            ShuffleHelper.Shuffle(shuffled, ShuffleHelper.SeedFor(attemptId, question.Id));
            question.Answers = shuffled;
        }

        var current = await db.ExamAttemptAnswers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.AttemptId == attemptId && x.QuestionId == question.Id, cancellationToken);

        return new QuestionPayload(index, snapshots.Count, question, current);
    }

    public async Task SaveAnswerAsync(SaveAnswerRequest request, Guid userId, CancellationToken cancellationToken = default)
    {
        var attempt = await db.ExamAttempts.AsNoTracking()
            .Include(x => x.Exam)
            .FirstOrDefaultAsync(x => x.Id == request.AttemptId && x.UserId == userId && x.Status == AttemptStatus.InProgress, cancellationToken);
        if (attempt is null) throw new InvalidOperationException("Lượt thi không còn hoạt động.");

        if (attempt.Exam is not null && DateTime.UtcNow > attempt.StartedAt.AddMinutes(attempt.Exam.DurationMinutes).Add(AnswerGrace))
        {
            throw new InvalidOperationException("Đã hết thời gian làm bài.");
        }

        var inAttempt = await db.ExamQuestionSnapshots.AnyAsync(x => x.AttemptId == request.AttemptId && x.QuestionId == request.QuestionId, cancellationToken);
        if (!inAttempt) throw new InvalidOperationException("Câu hỏi không thuộc lượt thi hiện tại.");

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
        var gate = GateFor($"submit:{attemptId}");
        await gate.WaitAsync(cancellationToken);
        try { await SubmitCoreAsync(attemptId, userId, timedOut, cancellationToken); }
        finally { gate.Release(); }
    }

    private async Task SubmitCoreAsync(Guid attemptId, Guid userId, bool timedOut, CancellationToken cancellationToken)
    {
        var attempt = await db.ExamAttempts
            .Include(x => x.QuestionSnapshots.OrderBy(s => s.OrderIndex))
            .Include(x => x.Answers)
            .FirstOrDefaultAsync(x => x.Id == attemptId && x.UserId == userId, cancellationToken);
        if (attempt is null || attempt.Status is not AttemptStatus.InProgress and not AttemptStatus.Flagged) return;

        var wasFlagged = attempt.Status == AttemptStatus.Flagged;
        if (!timedOut && attempt.Status == AttemptStatus.InProgress)
        {
            var examForDeadline = await db.Exams.AsNoTracking().FirstOrDefaultAsync(x => x.Id == attempt.ExamId, cancellationToken);
            if (examForDeadline is not null && DateTime.UtcNow > attempt.StartedAt.AddMinutes(examForDeadline.DurationMinutes).Add(AnswerGrace))
            {
                timedOut = true;
            }
        }
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

        var exam = await db.Exams.AsNoTracking().FirstAsync(x => x.Id == attempt.ExamId, cancellationToken);
        await academicRecordService.RecalculateAsync(userId, exam.SubjectId, attempt.SubmittedAt ?? DateTime.UtcNow, cancellationToken);
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

    public async Task ReportQuestionAsync(ReportQuestionRequest request, Guid userId, CancellationToken cancellationToken = default)
    {
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? "Câu hỏi có vấn đề" : request.Reason.Trim();
        if (reason.Length > 500) reason = reason[..500];

        var ownsAttempt = await db.ExamAttempts.AnyAsync(
            x => x.Id == request.AttemptId && x.UserId == userId && x.Status == AttemptStatus.InProgress,
            cancellationToken);
        if (!ownsAttempt)
        {
            throw new InvalidOperationException("Không thể báo lỗi khi bài thi không còn hoạt động.");
        }

        var inAttempt = await db.ExamQuestionSnapshots.AnyAsync(
            x => x.AttemptId == request.AttemptId && x.QuestionId == request.QuestionId,
            cancellationToken);
        if (!inAttempt)
        {
            throw new InvalidOperationException("Câu hỏi không thuộc lượt thi hiện tại.");
        }

        var alreadyReported = await db.QuestionReports.AnyAsync(
            x => x.QuestionId == request.QuestionId &&
                 x.ReportedByUserId == userId &&
                 x.Status == "Pending",
            cancellationToken);
        if (alreadyReported)
        {
            throw new InvalidOperationException("Bạn đã gửi báo lỗi cho câu hỏi này.");
        }

        var question = await db.Questions.FirstOrDefaultAsync(x => x.Id == request.QuestionId, cancellationToken);
        if (question is null)
        {
            throw new InvalidOperationException("Không tìm thấy câu hỏi.");
        }

        db.QuestionReports.Add(new QuestionReport
        {
            QuestionId = request.QuestionId,
            ReportedByUserId = userId,
            Reason = reason,
            Status = "Pending"
        });
        question.ReportCount++;
        await db.SaveChangesAsync(cancellationToken);
    }
}
