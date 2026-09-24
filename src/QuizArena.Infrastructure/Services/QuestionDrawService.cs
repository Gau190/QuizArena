using System.Security.Cryptography;
using QuizArena.Core.Entities;
using QuizArena.Core.Enums;
using QuizArena.Core.Interfaces;
using QuizArena.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace QuizArena.Infrastructure.Services;

public class QuestionDrawService(QuizArenaDbContext db) : IQuestionDrawService
{
    public async Task<List<Question>> DrawQuestionsAsync(int subjectId, int count, CancellationToken cancellationToken = default)
    {
        var ids = await db.Questions.AsNoTracking()
            .Where(q => q.SubjectId == subjectId && q.IsActive && q.IsApproved)
            .Select(q => q.Id)
            .ToListAsync(cancellationToken);

        Shuffle(ids);
        var selectedIds = ids.Take(count).ToList();

        return await db.Questions.AsNoTracking()
            .Include(q => q.Answers.OrderBy(a => a.OrderIndex))
            .Where(q => selectedIds.Contains(q.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Question>> DrawQuestionsByDifficultyAsync(int subjectId, int easyCount, int mediumCount, int hardCount, CancellationToken cancellationToken = default)
    {
        var selectedIds = new List<int>();
        selectedIds.AddRange(await DrawIdsAsync(subjectId, Difficulty.Easy, easyCount, cancellationToken));
        selectedIds.AddRange(await DrawIdsAsync(subjectId, Difficulty.Medium, mediumCount, cancellationToken));
        selectedIds.AddRange(await DrawIdsAsync(subjectId, Difficulty.Hard, hardCount, cancellationToken));
        Shuffle(selectedIds);

        return await db.Questions.AsNoTracking()
            .Include(q => q.Answers.OrderBy(a => a.OrderIndex))
            .Where(q => selectedIds.Contains(q.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Question>> DrawQuestionsByPointsAsync(int subjectId, decimal targetPoints, CancellationToken cancellationToken = default)
    {
        var questions = await db.Questions.AsNoTracking()
            .Include(q => q.Answers.OrderBy(a => a.OrderIndex))
            .Where(q => q.SubjectId == subjectId && q.IsActive && q.IsApproved)
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

    private async Task<List<int>> DrawIdsAsync(int subjectId, Difficulty difficulty, int count, CancellationToken cancellationToken)
    {
        if (count <= 0) return [];

        var ids = await db.Questions.AsNoTracking()
            .Where(q => q.SubjectId == subjectId && q.IsActive && q.IsApproved && q.Difficulty == difficulty)
            .Select(q => q.Id)
            .ToListAsync(cancellationToken);
        Shuffle(ids);
        return ids.Take(count).ToList();
    }
}
