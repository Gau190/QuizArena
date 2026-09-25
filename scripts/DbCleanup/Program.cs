using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

// Công cụ một lần: khảo sát / dọn dữ liệu QA và dữ liệu mang tên cũ trên CSDL đang chạy.
// Dùng: DbTool <đường dẫn .env.render> survey|dry|apply
var envFile = args[0]; var mode = args[1];
var line = File.ReadAllLines(envFile).First(l => l.StartsWith("ConnectionStrings__Default="));
var cs = line["ConnectionStrings__Default=".Length..].Trim().Trim('"');
await using var con = new SqlConnection(cs);
await con.OpenAsync();

async Task<int> Scalar(SqlTransaction? tx, string sql) { await using var c = new SqlCommand(sql, con, tx); return Convert.ToInt32(await c.ExecuteScalarAsync()); }
async Task<int> Exec(SqlTransaction tx, string sql) { await using var c = new SqlCommand(sql, con, tx); return await c.ExecuteNonQueryAsync(); }
async Task<List<string>> Rows(SqlTransaction? tx, string sql) { var l = new List<string>(); await using var c = new SqlCommand(sql, con, tx); await using var r = await c.ExecuteReaderAsync(); while (await r.ReadAsync()) l.Add(string.Join(" | ", Enumerable.Range(0, r.FieldCount).Select(i => r.IsDBNull(i) ? "NULL" : r.GetValue(i).ToString()))); return l; }

const string qaUsers = "SELECT Id FROM Users WHERE Username LIKE 'qa%[_]gv' OR Username LIKE 'qa%[_]ts_' OR Email LIKE '%@qa.test'";
const string qaSubjects = "SELECT Id FROM Subjects WHERE Name LIKE 'QA Môn %'";
const string badExams = $"SELECT Id FROM Exams WHERE Title LIKE 'QA %' OR SubjectId IN ({qaSubjects})";
const string badQuestions = $"SELECT Id FROM Questions WHERE SubjectId IN ({qaSubjects})";
const string badAttempts = $"SELECT Id FROM ExamAttempts WHERE ExamId IN ({badExams}) OR UserId IN ({qaUsers})";

if (mode == "tables")
{
    foreach (var r in await Rows(null, "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE' ORDER BY 1")) Console.WriteLine(r);
    return;
}
if (mode == "terms")
{
    foreach (var (t, c) in new[] { ("Notifications", "Title"), ("Notifications", "Body"), ("Exams", "Description"), ("Exams", "Title"), ("Subjects", "Description"), ("Organizations", "Description"), ("Questions", "Content"), ("Questions", "Explanation") })
    {
        try { Console.WriteLine($"{t}.{c}: " + await Scalar(null, $"SELECT COUNT(*) FROM {t} WHERE {c} LIKE N'%Giảng viên%' OR {c} LIKE N'%giảng viên%' OR {c} LIKE N'%Học sinh%' OR {c} LIKE N'%học sinh%' OR {c} LIKE N'%GVCN%' OR {c} LIKE N'%ExamHub%'")); }
        catch (Exception e) { Console.WriteLine($"{t}.{c}: (bỏ qua) {e.Message.Split('\n')[0]}"); }
    }
    return;
}
if (mode == "survey")
{
    Console.WriteLine("Users tổng: " + await Scalar(null, "SELECT COUNT(*) FROM Users"));
    Console.WriteLine("Users QA: " + await Scalar(null, $"SELECT COUNT(*) FROM ({qaUsers}) t"));
    Console.WriteLine("Users email @examhub: " + await Scalar(null, "SELECT COUNT(*) FROM Users WHERE Email LIKE '%examhub%'"));
    Console.WriteLine("Subjects QA: " + await Scalar(null, $"SELECT COUNT(*) FROM ({qaSubjects}) t"));
    Console.WriteLine("Questions QA: " + await Scalar(null, $"SELECT COUNT(*) FROM ({badQuestions}) t"));
    Console.WriteLine("Exams QA/AUDIT: " + await Scalar(null, $"SELECT COUNT(*) FROM ({badExams}) t"));
    foreach (var r in await Rows(null, $"SELECT Id, Title, IsActive FROM Exams WHERE Id IN ({badExams})")) Console.WriteLine("  exam: " + r);
    Console.WriteLine("Attempts liên quan: " + await Scalar(null, $"SELECT COUNT(*) FROM ({badAttempts}) t"));
    Console.WriteLine("Classes QA-%: " + await Scalar(null, "SELECT COUNT(*) FROM Classes WHERE Name LIKE 'QA-%'"));
    Console.WriteLine("Notifications 'Kiểm tra hệ thống': " + await Scalar(null, "SELECT COUNT(*) FROM Notifications WHERE Title LIKE N'Kiểm tra hệ thống%'"));
    Console.WriteLine("Student code HS-: " + await Scalar(null, "SELECT COUNT(*) FROM StudentProfiles WHERE StudentCode LIKE 'HS-%'"));
    Console.WriteLine("Exams tên chứa mã HTML: " + await Scalar(null, "SELECT COUNT(*) FROM Exams WHERE Title LIKE '%&#%'"));
    Console.WriteLine("Subjects tên chứa mã HTML: " + await Scalar(null, "SELECT COUNT(*) FROM Subjects WHERE Name LIKE '%&#%' OR Description LIKE '%&#%'"));
    Console.WriteLine("Questions chứa mã HTML: " + await Scalar(null, "SELECT COUNT(*) FROM Questions WHERE Content LIKE '%&#%'"));
    foreach (var r in await Rows(null, "SELECT TOP 1 Name, Code, Email, Website, LEFT(Description,80) FROM Organizations")) Console.WriteLine("Organization: " + r);
    return;
}

await using var tx = con.BeginTransaction();
var log = new List<string>();
async Task Step(string name, string sql) { log.Add($"{name}: {await Exec(tx, sql)}"); }
await Step("ExamAttemptAnswers", $"DELETE FROM ExamAttemptAnswers WHERE AttemptId IN ({badAttempts})");
await Step("ExamQuestionSnapshots", $"DELETE FROM ExamQuestionSnapshots WHERE AttemptId IN ({badAttempts})");
await Step("ExamAttempts", $"DELETE FROM ExamAttempts WHERE Id IN ({badAttempts})");
await Step("QuestionReports", $"DELETE FROM QuestionReports WHERE QuestionId IN ({badQuestions}) OR ReportedByUserId IN ({qaUsers})");
await Step("ExamClasses", $"DELETE FROM ExamClasses WHERE ExamId IN ({badExams})");
await Step("ExamRooms", $"DELETE FROM ExamRooms WHERE ExamId IN ({badExams})");
await Step("Exams", $"DELETE FROM Exams WHERE Id IN ({badExams})");
await Step("Answers", $"DELETE FROM Answers WHERE QuestionId IN ({badQuestions})");
await Step("Questions", $"DELETE FROM Questions WHERE Id IN ({badQuestions})");
await Step("AcademicRecords", $"DELETE FROM AcademicRecords WHERE StudentId IN ({qaUsers}) OR SubjectId IN ({qaSubjects})");
await Step("ExamBlueprintItems", $"DELETE FROM ExamBlueprintItems WHERE SubjectId IN ({qaSubjects}) OR CreatedBy IN ({qaUsers})");
await Step("Subjects", $"DELETE FROM Subjects WHERE Id IN ({qaSubjects})");
await Step("ClassStudents", $"DELETE FROM ClassStudents WHERE StudentId IN ({qaUsers})");
await Step("Classes", "DELETE FROM Classes WHERE Name LIKE 'QA-%'");
await Step("StudentProfiles", $"DELETE FROM StudentProfiles WHERE UserId IN ({qaUsers})");
await Step("TeacherProfiles", $"DELETE FROM TeacherProfiles WHERE UserId IN ({qaUsers})");
await Step("ActiveSessions", $"DELETE FROM ActiveSessions WHERE UserId IN ({qaUsers})");
await Step("Users QA", $"DELETE FROM Users WHERE Id IN ({qaUsers})");
await Step("Ẩn kỳ thi AUDIT_EXAM (giữ điểm của thí sinh)", "UPDATE Exams SET IsActive = 0 WHERE Title LIKE 'AUDIT[_]EXAM[_]%' AND IsActive = 1");
await Step("Notifications thử", "DELETE FROM Notifications WHERE Title LIKE N'Kiểm tra hệ thống%'");
await Step("Email @examhub → @quizarena", "UPDATE Users SET Email = REPLACE(Email, '@examhub.', '@quizarena.') WHERE Email LIKE '%@examhub.%'");
await Step("Organization email/website", "UPDATE Organizations SET Email = REPLACE(Email, 'examhub', 'quizarena'), Website = REPLACE(Website, 'examhub', 'quizarena') WHERE Email LIKE '%examhub%' OR Website LIKE '%examhub%'");
await Step("Mã thí sinh HS- → TS-", "UPDATE StudentProfiles SET StudentCode = 'TS-' + SUBSTRING(StudentCode, 4, 50) WHERE StudentCode LIKE 'HS-%'");

// mã HTML bị mã hoá hai lần trong dữ liệu chữ
async Task Decode(string table, string col)
{
    var rows = new List<(int id, string v)>();
    await using (var c = new SqlCommand($"SELECT Id, {col} FROM {table} WHERE {col} LIKE '%&#%' OR {col} LIKE '%&amp;%'", con, tx))
    await using (var r = await c.ExecuteReaderAsync()) while (await r.ReadAsync()) rows.Add((r.GetInt32(0), r.GetString(1)));
    foreach (var (id, v) in rows)
    {
        var d = WebUtility.HtmlDecode(v);
        if (d == v) continue;
        await using var u = new SqlCommand($"UPDATE {table} SET {col} = @v WHERE Id = @id", con, tx);
        u.Parameters.AddWithValue("@v", d); u.Parameters.AddWithValue("@id", id); await u.ExecuteNonQueryAsync();
    }
    log.Add($"Giải mã HTML {table}.{col}: {rows.Count} dòng");
}
await Decode("Exams", "Title"); await Decode("Subjects", "Name"); await Decode("Subjects", "Description");

log.ForEach(Console.WriteLine);
Console.WriteLine("Còn lại: users QA=" + await Scalar(tx, $"SELECT COUNT(*) FROM ({qaUsers}) t") + ", exams QA/AUDIT=" + await Scalar(tx, $"SELECT COUNT(*) FROM ({badExams}) t") + ", users tổng=" + await Scalar(tx, "SELECT COUNT(*) FROM Users"));
if (mode == "apply") { await tx.CommitAsync(); Console.WriteLine("ĐÃ COMMIT"); } else { await tx.RollbackAsync(); Console.WriteLine("Chạy thử, đã ROLLBACK"); }
