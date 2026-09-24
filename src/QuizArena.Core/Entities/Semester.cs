namespace QuizArena.Core.Entities;

public class Semester
{
    public int Id { get; set; }
    public int AcademicYearId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public bool IsActive { get; set; }

    public AcademicYear? AcademicYear { get; set; }
}
