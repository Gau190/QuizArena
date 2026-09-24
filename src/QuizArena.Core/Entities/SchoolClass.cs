namespace QuizArena.Core.Entities;

public class SchoolClass
{
    public int Id { get; set; }
    public int OrganizationId { get; set; }
    public int AcademicYearId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Grade { get; set; }
    public string? Track { get; set; }
    public string? Room { get; set; }
    public Guid? HomeTeacherId { get; set; }
    public int MaxStudents { get; set; } = 45;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Organization? Organization { get; set; }
    public AcademicYear? AcademicYear { get; set; }
    public User? HomeTeacher { get; set; }
    public ICollection<ClassStudent> Students { get; set; } = new List<ClassStudent>();
    public ICollection<ExamRoom> ExamRooms { get; set; } = new List<ExamRoom>();
}
