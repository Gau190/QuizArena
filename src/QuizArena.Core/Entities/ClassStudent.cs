namespace QuizArena.Core.Entities;

public class ClassStudent
{
    public int ClassId { get; set; }
    public Guid StudentId { get; set; }
    public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    public SchoolClass? Class { get; set; }
    public User? Student { get; set; }
}
