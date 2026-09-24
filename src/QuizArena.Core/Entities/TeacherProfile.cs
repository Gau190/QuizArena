namespace QuizArena.Core.Entities;

public class TeacherProfile
{
    public Guid UserId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string? Specialization { get; set; }
    public string? Degree { get; set; }
    public DateOnly? JoinDate { get; set; }

    public User? User { get; set; }
}
