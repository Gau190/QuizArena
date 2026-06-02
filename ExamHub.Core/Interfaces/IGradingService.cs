using ExamHub.Core.Entities;
using ExamHub.Core.Models;

namespace ExamHub.Core.Interfaces;

public interface IGradingService
{
    GradeResult GradeAnswer(Question question, ExamAttemptAnswer studentAnswer);
}
