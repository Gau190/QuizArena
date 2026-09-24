using QuizArena.Infrastructure.Configuration;
using QuizArena.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

DotEnv.Load();
var connectionString = args.FirstOrDefault()
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__Default")
    ?? Environment.GetEnvironmentVariable("QUIZARENA_CONNECTION");

if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("Missing connection string. Pass it as the first argument or set ConnectionStrings__Default.");
    return 1;
}

var options = new DbContextOptionsBuilder<QuizArenaDbContext>()
    .UseSqlServer(connectionString)
    .Options;

await using var db = new QuizArenaDbContext(options);
Console.WriteLine("Applying migrations...");
await db.Database.MigrateAsync();

Console.WriteLine("Seeding data...");
await SeedData.EnsureSeededAsync(db);

var users = await db.Users.CountAsync();
var subjects = await db.Subjects.CountAsync();
var questions = await db.Questions.CountAsync();
var exams = await db.Exams.CountAsync();

Console.WriteLine($"Done. Users={users}, Subjects={subjects}, Questions={questions}, Exams={exams}");
return 0;
