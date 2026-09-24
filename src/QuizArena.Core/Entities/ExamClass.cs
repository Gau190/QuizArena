namespace QuizArena.Core.Entities;

public class ExamClass
{
    public int ExamId { get; set; }
    public int ClassId { get; set; }

    public Exam? Exam { get; set; }
    public SchoolClass? Class { get; set; }
}
