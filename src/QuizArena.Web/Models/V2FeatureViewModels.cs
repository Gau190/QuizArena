using QuizArena.Core.Entities;
using QuizArena.Core.Enums;

namespace QuizArena.Web.Models;

public sealed record MetricCard(string Label, string Value, string Hint, string Tone = "");

public sealed record SubjectMetric(string Name, decimal AverageScore, int Attempts, string Color);

public sealed record ActivityItem(string Icon, string Text, string TimeAgo, string Tone = "");

public sealed record AdminDashboardViewModel(
    IReadOnlyList<MetricCard> Metrics,
    IReadOnlyList<SubjectMetric> SubjectMetrics,
    IReadOnlyList<ActivityItem> Activities,
    IReadOnlyList<Exam> UpcomingExams);

public sealed record ClassOverview(
    int Id,
    string Name,
    string Grade,
    string Track,
    string HomeTeacher,
    Guid? HomeTeacherId,
    string Room,
    int MaxStudents,
    int StudentCount,
    decimal AverageScore,
    bool IsSelected = false);

public sealed record StudentAssignmentRow(
    User Student,
    string StudentCode,
    int? ClassId,
    string ClassName,
    bool IsActive);

public sealed record ClassManagementViewModel(
    IReadOnlyList<ClassOverview> Classes,
    IReadOnlyList<StudentAssignmentRow> Students,
    IReadOnlyList<User> Teachers,
    IReadOnlyList<AcademicYear> AcademicYears,
    int CurrentAcademicYearId);

public sealed record CalendarEvent(
    int Day,
    string Title,
    string Subject,
    string Time,
    string Color,
    bool IsMuted = false,
    DateTime? StartLocal = null);

public sealed record CalendarLegendItem(string Label, string Color);

public sealed record CalendarListRow(
    string Title,
    string Subject,
    string ClassNames,
    string TimeRange,
    string Color,
    bool IsPast);

public sealed record ExamCalendarViewModel(
    string MonthTitle,
    IReadOnlyList<CalendarEvent> Events,
    IReadOnlyList<Exam> UpcomingExams,
    int Year,
    int Month,
    int DaysInMonth,
    int LeadingBlankDays,
    IReadOnlyList<CalendarLegendItem> Legend,
    string ViewMode = "month",
    int WeekStartDay = 1,
    int DaysInWeek = 7,
    IReadOnlyList<CalendarEvent>? WeekEvents = null,
    IReadOnlyList<CalendarListRow>? ListRows = null);

public sealed record StudentScoreRow(string Subject, decimal AverageScore, int Attempts, string LastAttempt, string Color);

public sealed record StudentMonthlyPoint(string Label, decimal AverageScore);

public sealed record StudentProfileViewModel(
    User Student,
    StudentProfile? Profile,
    IReadOnlyList<ExamAttempt> Attempts,
    IReadOnlyList<StudentScoreRow> SubjectScores,
    decimal AverageScore,
    int CompletedAttempts,
    int PassRate,
    string ClassName,
    string StudentCode,
    int ClassRank,
    int ClassStudentCount,
    decimal ClassAverageScore,
    IReadOnlyList<StudentMonthlyPoint> MonthlyScores);

public sealed record TranscriptSubjectRow(string Subject, decimal Semester1, decimal Semester2, decimal FinalScore, string Rank, int Attempts);

public sealed record TranscriptViewModel(
    StudentProfileViewModel Profile,
    IReadOnlyList<TranscriptSubjectRow> Subjects,
    string Term = "2",
    string RankLabel = "—");

public sealed record NotificationItem(
    string Title,
    string Body,
    string Category,
    string Time,
    bool Unread,
    string Tone);

public sealed record NotificationsViewModel(
    int UnreadCount,
    IReadOnlyList<NotificationItem> Items);

public sealed record AnalyticsReportViewModel(
    IReadOnlyList<MetricCard> Metrics,
    IReadOnlyList<SubjectMetric> SubjectMetrics,
    IReadOnlyList<ActivityItem> Insights,
    IReadOnlyList<MonthlyScorePoint> MonthlyScores,
    IReadOnlyList<ScoreDistributionPoint> ScoreDistribution,
    IReadOnlyList<ClassScoreMetric> ClassMetrics,
    IReadOnlyList<TopStudentReportRow> TopStudents);

public sealed record MonthlyScorePoint(string Label, decimal AverageScore, int Attempts);

public sealed record ScoreDistributionPoint(string Label, int Count, int Percent, string Color);

public sealed record ClassScoreMetric(string ClassName, decimal AverageScore, int Attempts, int PassRate, string Color);

public sealed record TopStudentReportRow(int Rank, string StudentName, string ClassName, decimal AverageScore, int Attempts, int PassRate);

public sealed record QuestionResultStat(
    int QuestionId,
    string Content,
    QuestionType Type,
    int Total,
    int Correct);

public sealed record WeakQuestionRow(
    int QuestionId,
    string Subject,
    string Content,
    int Total,
    int Correct,
    int CorrectRate,
    int ReportCount);

public sealed record TeacherProfileRow(
    User Teacher,
    string EmployeeCode,
    string Specialization,
    string Degree,
    int QuestionCount,
    int ExamCount,
    int ClassCount);

public sealed record TeacherProfilesViewModel(
    IReadOnlyList<TeacherProfileRow> Teachers);

public sealed record AcademicTermRow(
    string AcademicYear,
    string Semester,
    string DateRange,
    bool IsActive,
    int ExamCount);

public sealed record AcademicYearsViewModel(
    IReadOnlyList<AcademicTermRow> Terms,
    IReadOnlyList<MetricCard> Metrics);

public sealed record ExamRoomRow(
    int Id,
    int? ExamId,
    int? ClassId,
    Guid? ProctorId,
    string ExamName,
    string ClassName,
    string Room,
    string Shift,
    string Proctor,
    int Students,
    string Status);

public sealed record ExamRoomsViewModel(
    IReadOnlyList<ExamRoomRow> Rooms,
    IReadOnlyList<Exam> Exams,
    IReadOnlyList<SchoolClass> Classes,
    IReadOnlyList<User> Teachers);

public sealed record QuestionReportRow(
    int QuestionId,
    string Subject,
    string Content,
    string Reason,
    string Reporter,
    string Status,
    string CreatedAt);

public sealed record QuestionReportsViewModel(
    IReadOnlyList<QuestionReportRow> Reports);

public sealed record ExamBlueprintRow(
    string Chapter,
    int EasyCount,
    int MediumCount,
    int HardCount,
    decimal Points);

public sealed record ExamBlueprintViewModel(
    IReadOnlyList<Subject> Subjects,
    IReadOnlyList<ExamBlueprintRow> Rows,
    int TotalQuestions,
    decimal TotalPoints,
    int SelectedSubjectId,
    GenerateMode GenerateMode,
    decimal TargetTotalPoints);
