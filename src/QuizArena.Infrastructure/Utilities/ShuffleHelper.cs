namespace QuizArena.Infrastructure.Utilities;

internal static class ShuffleHelper
{
    public static void Shuffle<T>(IList<T> list, int seed)
    {
        var rng = new Random(seed);
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    public static int SeedFor(Guid attemptId, int questionId) => HashCode.Combine(attemptId, questionId);
}