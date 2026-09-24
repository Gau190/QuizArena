using QuizArena.Core.Entities;
using QuizArena.Core.Models;

namespace QuizArena.Core.Interfaces;

public interface IGradingService
{
    GradeResult GradeAnswer(Question question, ExamAttemptAnswer studentAnswer);
}
