using System.Security.Cryptography;
using ExamHub.Core.Entities;
using ExamHub.Core.Interfaces;
using ExamHub.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ExamHub.Infrastructure.Services;

public class QuestionDrawService(ExamHubDbContext db) : IQuestionDrawService
{
    public async Task<List<Question>> DrawQuestionsAsync(int subjectId, int count, CancellationToken cancellationToken = default)
    {
        var ids = await db.Questions.AsNoTracking()
            .Where(q => q.SubjectId == subjectId && q.IsActive)
            .Select(q => q.Id)
            .ToListAsync(cancellationToken);

        Shuffle(ids);
        var selectedIds = ids.Take(count).ToList();

        return await db.Questions.AsNoTracking()
            .Include(q => q.Answers.OrderBy(a => a.OrderIndex))
            .Where(q => selectedIds.Contains(q.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Question>> DrawQuestionsByPointsAsync(int subjectId, decimal targetPoints, CancellationToken cancellationToken = default)
    {
        var questions = await db.Questions.AsNoTracking()
            .Include(q => q.Answers.OrderBy(a => a.OrderIndex))
            .Where(q => q.SubjectId == subjectId && q.IsActive)
            .ToListAsync(cancellationToken);

        Shuffle(questions);
        var total = 0m;
        var selected = new List<Question>();
        foreach (var question in questions)
        {
            if (total >= targetPoints) break;
            selected.Add(question);
            total += question.Points;
        }

        return selected;
    }

    private static void Shuffle<T>(IList<T> items)
    {
        for (var i = items.Count - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }
}
