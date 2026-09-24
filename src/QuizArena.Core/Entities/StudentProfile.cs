namespace QuizArena.Core.Entities;

public class StudentProfile
{
    public Guid UserId { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string? ParentName { get; set; }
    public string? ParentPhone { get; set; }
    public string? ParentEmail { get; set; }
    public string? Address { get; set; }
    public string Conduct { get; set; } = "Tốt";
    public string? AcademicLevel { get; set; }
    public string? Notes { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public User? User { get; set; }
}
