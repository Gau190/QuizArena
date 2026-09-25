using QuizArena.Core.Enums;

namespace QuizArena.Web.Utilities;

/// <summary>Nhãn tiếng Việt cho các enum hiển thị trên giao diện.</summary>
public static class Labels
{
    public static string Of(QuestionType type) => type switch
    {
        QuestionType.SingleChoice => "Một đáp án",
        QuestionType.MultipleChoice => "Nhiều đáp án",
        QuestionType.TextAnswer => "Điền đáp án",
        _ => type.ToString()
    };

    public static string Of(Difficulty difficulty) => difficulty switch
    {
        Difficulty.Easy => "Dễ",
        Difficulty.Medium => "Trung bình",
        Difficulty.Hard => "Khó",
        _ => difficulty.ToString()
    };

    public static string Of(AttemptStatus status) => status switch
    {
        AttemptStatus.InProgress => "Đang làm",
        AttemptStatus.Submitted => "Đã nộp",
        AttemptStatus.TimedOut => "Hết giờ",
        AttemptStatus.Flagged => "Bị gắn cờ",
        _ => status.ToString()
    };

    public static string Of(ExamStatus status) => status switch
    {
        ExamStatus.Draft => "Bản nháp",
        ExamStatus.Published => "Đã xuất bản",
        ExamStatus.Active => "Đang diễn ra",
        ExamStatus.Closed => "Đã đóng",
        _ => status.ToString()
    };

    public static string Of(UserRole role) => role switch
    {
        UserRole.Admin => "Quản trị viên",
        UserRole.Teacher => "Giáo viên",
        UserRole.Student => "Thí sinh",
        _ => role.ToString()
    };
}
