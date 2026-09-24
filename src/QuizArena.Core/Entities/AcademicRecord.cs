namespace QuizArena.Core.Entities;

public class AcademicRecord
{
    public int Id { get; set; }
    public Guid StudentId { get; set; }
    public int SubjectId { get; set; }
    public int SemesterId { get; set; }
    public decimal? AverageScore { get; set; }
    public string? Rank { get; set; }
    public int TotalAttempts { get; set; }
    public decimal? BestScore { get; set; }
    public DateTime? LastExamDate { get; set; }
    public string? TeacherComment { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public User? Student { get; set; }
    public Subject? Subject { get; set; }
    public Semester? Semester { get; set; }
}