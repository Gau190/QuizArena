# QUIZARENA — SPEC ĐẦY ĐỦ V2 (Nâng cấp cho Trường học / Trung tâm giáo dục)

> **Dành cho AI Agent:** Đọc toàn bộ file này trước khi viết bất kỳ dòng code nào.  
> Công nghệ: **ASP.NET Core 8 MVC + Web API**, **Entity Framework Core 8**, **MS SQL Server 2022**, **SignalR** (timer đồng bộ), **BCrypt.Net**, **JWT Bearer**.

---

## MỤC LỤC

1. [Tổng quan kiến trúc hệ thống](#1-tổng-quan-kiến-trúc)
2. [Schema Database đầy đủ (V2)](#2-schema-database)
3. [Seed Data](#3-seed-data)
4. [Phân quyền & Authentication](#4-phân-quyền--authentication)
5. [Giai đoạn 1 — Khởi tạo & Database](#5-giai-đoạn-1)
6. [Giai đoạn 2 — Bảo mật chuyên sâu](#6-giai-đoạn-2)
7. [Giai đoạn 3 — Quản lý tổ chức (Admin)](#7-giai-đoạn-3)
8. [Giai đoạn 4 — Nghiệp vụ Giáo viên](#8-giai-đoạn-4)
9. [Giai đoạn 5 — Nghiệp vụ Thí sinh](#9-giai-đoạn-5)
10. [Giai đoạn 6 — Tính năng Mở rộng (Nâng cấp V2)](#10-giai-đoạn-6-mở-rộng-v2)
11. [Giai đoạn 7 — Phân tích & Báo cáo](#11-giai-đoạn-7-báo-cáo)
12. [Giai đoạn 8 — Deploy & CI/CD](#12-giai-đoạn-8-deploy)
13. [API Endpoints đầy đủ](#13-api-endpoints)
14. [Checklist nghiệm thu](#14-checklist-nghiệm-thu)

---

## 1. TỔNG QUAN KIẾN TRÚC

### Luồng dữ liệu tổng thể

```
Browser ─► ASP.NET Core MVC (Views/Controllers)
              │
              ├─► Web API Controllers (JWT Bearer)
              │       │
              │       ├─► Service Layer (Business Logic)
              │       │       │
              │       │       └─► Repository Layer (EF Core + AsNoTracking)
              │       │               │
              │       │               └─► MS SQL Server
              │       │
              │       └─► IMemoryCache (Subjects, Questions list)
              │
              └─► SignalR Hub (ExamTimerHub) — đồng bộ server-time
```

### Các màn hình đã thiết kế (11 màn hình)

| Màn hình | File | Vai trò | Trạng thái |
|---|---|---|---|
| Đăng nhập (tự detect role) | `screen_login.html` | Tất cả | Cốt lõi |
| Admin Dashboard | `screen_C_admin_dashboard.html` | Admin | Cốt lõi |
| Quản lý lớp học | `screen_class_management_v2.html` | Admin | Cốt lõi |
| Tạo kỳ thi | `screen_A_create_exam.html` | Giáo viên | Cốt lõi |
| Lịch thi | `screen_B_exam_calendar.html` | GV/HS | Cốt lõi |
| Hồ sơ học sinh | `screen_student_profile_v2.html` | GV/Admin | Cốt lõi |
| Học bạ điện tử | `man_hoc_ba_dien_tu.html` | GV/Admin | Mới V2 |
| Hồ sơ giáo viên | _(cần tạo)_ | Admin | Cốt lõi |
| Thông báo | `screen_notifications_v2.html` | Tất cả | Mới V2 |
| Báo cáo & Phân tích | `man_analytics_report.html` | GV/Admin | Mới V2 |
| Cài đặt tổ chức | `man_cai_dat_to_chuc.html` | Admin | Mới V2 |

---

## 2. SCHEMA DATABASE

### Nhóm 1 — Identity & Auth

```sql
CREATE TABLE Organizations (
    Id          INT IDENTITY PRIMARY KEY,
    Name        NVARCHAR(200) NOT NULL,          -- Tên trường/trung tâm
    Code        NVARCHAR(50) UNIQUE NOT NULL,    -- THPT-CVA-HN01
    Address     NVARCHAR(500),
    Phone       NVARCHAR(20),
    Email       NVARCHAR(200),
    Website     NVARCHAR(200),
    Description NVARCHAR(1000),
    LogoUrl     NVARCHAR(500),
    -- Cài đặt tổ chức (JSON blob hoặc cột riêng)
    AllowViewAnswerAfterSubmit BIT DEFAULT 0,
    AutoSubmitOnTimeout        BIT DEFAULT 1,
    AntiCheatTabSwitch         BIT DEFAULT 1,
    ShuffleQuestions           BIT DEFAULT 1,
    ShuffleAnswers             BIT DEFAULT 1,
    ShareQuestionBank          BIT DEFAULT 1,
    SendEmailOnResult          BIT DEFAULT 0,
    CreatedAt   DATETIME2 DEFAULT GETDATE()
);

CREATE TABLE Users (
    Id              INT IDENTITY PRIMARY KEY,
    OrganizationId  INT NOT NULL REFERENCES Organizations(Id),
    Username        NVARCHAR(100) UNIQUE NOT NULL,
    PasswordHash    NVARCHAR(255) NOT NULL,       -- BCrypt/Argon2, KHÔNG MD5
    FullName        NVARCHAR(200) NOT NULL,
    Email           NVARCHAR(200),
    Phone           NVARCHAR(20),
    Role            NVARCHAR(20) NOT NULL         -- 'Admin','Teacher','Student'
                    CHECK (Role IN ('Admin','Teacher','Student')),
    IsActive        BIT DEFAULT 1,
    AvatarInitials  NVARCHAR(3),                  -- VD: 'NT', 'GV'
    LastLoginAt     DATETIME2,
    CreatedAt       DATETIME2 DEFAULT GETDATE(),
    UpdatedAt       DATETIME2
);

CREATE TABLE UserSessions (
    Id          INT IDENTITY PRIMARY KEY,
    UserId      INT NOT NULL REFERENCES Users(Id),
    SessionToken NVARCHAR(500) NOT NULL,          -- JWT jti claim
    IpAddress   NVARCHAR(50),
    UserAgent   NVARCHAR(500),
    CreatedAt   DATETIME2 DEFAULT GETDATE(),
    ExpiresAt   DATETIME2 NOT NULL,
    IsRevoked   BIT DEFAULT 0
    -- Index: UserId, IsRevoked — dùng cho single-session middleware
);

CREATE TABLE AuditLogs (
    Id          BIGINT IDENTITY PRIMARY KEY,
    OrganizationId INT REFERENCES Organizations(Id),
    UserId      INT REFERENCES Users(Id),
    Action      NVARCHAR(100) NOT NULL,           -- 'LOGIN','EXPORT','DELETE_USER',...
    TargetType  NVARCHAR(100),                    -- 'User','Exam','Question',...
    TargetId    INT,
    Detail      NVARCHAR(2000),
    IpAddress   NVARCHAR(50),
    CreatedAt   DATETIME2 DEFAULT GETDATE()
);
```

### Nhóm 2 — Cấu trúc học tập

```sql
CREATE TABLE AcademicYears (
    Id              INT IDENTITY PRIMARY KEY,
    OrganizationId  INT NOT NULL REFERENCES Organizations(Id),
    Name            NVARCHAR(50) NOT NULL,        -- '2024-2025'
    StartDate       DATE NOT NULL,
    EndDate         DATE NOT NULL,
    IsCurrent       BIT DEFAULT 0,
    CreatedAt       DATETIME2 DEFAULT GETDATE()
);

CREATE TABLE Semesters (
    Id              INT IDENTITY PRIMARY KEY,
    AcademicYearId  INT NOT NULL REFERENCES AcademicYears(Id),
    Name            NVARCHAR(50) NOT NULL,        -- 'Học kỳ 1', 'Học kỳ 2'
    StartDate       DATE NOT NULL,
    EndDate         DATE NOT NULL,
    IsActive        BIT DEFAULT 0
);

CREATE TABLE Classes (
    Id              INT IDENTITY PRIMARY KEY,
    OrganizationId  INT NOT NULL REFERENCES Organizations(Id),
    AcademicYearId  INT NOT NULL REFERENCES AcademicYears(Id),
    Name            NVARCHAR(50) NOT NULL,        -- '12A1'
    Grade           NVARCHAR(10),                 -- '12', '11', '10'
    Track           NVARCHAR(100),                -- 'Khoa học tự nhiên'
    Room            NVARCHAR(50),                 -- 'Phòng 301'
    HomeTeacherId   INT REFERENCES Users(Id),     -- GVCN
    MaxStudents     INT DEFAULT 45,
    CreatedAt       DATETIME2 DEFAULT GETDATE()
);

CREATE TABLE ClassStudents (
    ClassId     INT NOT NULL REFERENCES Classes(Id),
    StudentId   INT NOT NULL REFERENCES Users(Id),
    EnrolledAt  DATETIME2 DEFAULT GETDATE(),
    IsActive    BIT DEFAULT 1,
    PRIMARY KEY (ClassId, StudentId)
);

CREATE TABLE StudentProfiles (
    UserId          INT PRIMARY KEY REFERENCES Users(Id),
    DateOfBirth     DATE,
    Gender          NVARCHAR(10),               -- 'Nam','Nữ','Khác'
    StudentCode     NVARCHAR(50) UNIQUE,        -- 'HS-2024-0089'
    ParentName      NVARCHAR(200),
    ParentPhone     NVARCHAR(20),
    ParentEmail     NVARCHAR(200),
    Address         NVARCHAR(500),
    Conduct         NVARCHAR(20) DEFAULT 'Tốt', -- 'Tốt','Khá','TB','Yếu'
    AcademicLevel   NVARCHAR(20),               -- 'Xuất sắc','Giỏi','Khá','TB','Yếu'
    Notes           NVARCHAR(2000),
    UpdatedAt       DATETIME2
);

CREATE TABLE TeacherProfiles (
    UserId          INT PRIMARY KEY REFERENCES Users(Id),
    EmployeeCode    NVARCHAR(50) UNIQUE,
    Specialization  NVARCHAR(200),              -- 'Toán học, Vật lý'
    Degree          NVARCHAR(100),              -- 'Thạc sĩ'
    JoinDate        DATE
);
```

### Nhóm 3 — Ngân hàng câu hỏi

```sql
CREATE TABLE Subjects (
    Id              INT IDENTITY PRIMARY KEY,
    OrganizationId  INT NOT NULL REFERENCES Organizations(Id),
    Name            NVARCHAR(200) NOT NULL,
    Code            NVARCHAR(50),
    ColorHex        NVARCHAR(7),                -- '#378ADD' — màu hiển thị trên lịch thi
    IsShared        BIT DEFAULT 0,             -- cho phép chia sẻ giữa GV trong tổ chức
    CreatedAt       DATETIME2 DEFAULT GETDATE()
);

CREATE TABLE Questions (
    Id              INT IDENTITY PRIMARY KEY,
    SubjectId       INT NOT NULL REFERENCES Subjects(Id),
    CreatedByUserId INT NOT NULL REFERENCES Users(Id),
    Type            NVARCHAR(20) NOT NULL
                    CHECK (Type IN ('SingleChoice','MultipleChoice','TextAnswer')),
    Content         NVARCHAR(MAX) NOT NULL,
    Explanation     NVARCHAR(MAX),              -- Giải thích đáp án đúng
    Difficulty      NVARCHAR(10) NOT NULL DEFAULT 'Medium'
                    CHECK (Difficulty IN ('Easy','Medium','Hard')),
    Tags            NVARCHAR(500),              -- 'Chương 1,Đại số' — phân loại theo chủ đề
    Chapter         NVARCHAR(200),
    IsApproved      BIT DEFAULT 1,             -- Giáo viên duyệt câu hỏi chia sẻ
    IsActive        BIT DEFAULT 1,
    TimesUsed       INT DEFAULT 0,
    CorrectRate     FLOAT DEFAULT 0,           -- % học sinh trả lời đúng (cập nhật sau mỗi kỳ thi)
    ReportCount     INT DEFAULT 0,             -- Số lần học sinh báo lỗi câu hỏi
    CreatedAt       DATETIME2 DEFAULT GETDATE(),
    UpdatedAt       DATETIME2
);

CREATE TABLE Answers (
    Id          INT IDENTITY PRIMARY KEY,
    QuestionId  INT NOT NULL REFERENCES Questions(Id) ON DELETE CASCADE,
    Content     NVARCHAR(MAX) NOT NULL,
    IsCorrect   BIT NOT NULL DEFAULT 0,
    OrderIndex  INT DEFAULT 0
);

CREATE TABLE QuestionReports (
    Id              INT IDENTITY PRIMARY KEY,
    QuestionId      INT NOT NULL REFERENCES Questions(Id),
    ReportedByUserId INT NOT NULL REFERENCES Users(Id),
    Reason          NVARCHAR(500),              -- 'Câu hỏi sai đáp án', 'Nội dung không rõ'
    Status          NVARCHAR(20) DEFAULT 'Pending'
                    CHECK (Status IN ('Pending','Reviewed','Fixed','Dismissed')),
    TeacherNote     NVARCHAR(1000),
    CreatedAt       DATETIME2 DEFAULT GETDATE(),
    ReviewedAt      DATETIME2
);
```

### Nhóm 4 — Kỳ thi & Làm bài

```sql
CREATE TABLE Exams (
    Id                  INT IDENTITY PRIMARY KEY,
    OrganizationId      INT NOT NULL REFERENCES Organizations(Id),
    SubjectId           INT NOT NULL REFERENCES Subjects(Id),
    CreatedByUserId     INT NOT NULL REFERENCES Users(Id),
    Title               NVARCHAR(500) NOT NULL,
    Description         NVARCHAR(2000),
    SemesterId          INT REFERENCES Semesters(Id),
    DurationMinutes     INT NOT NULL,
    TotalQuestions      INT NOT NULL,
    TotalScore          FLOAT DEFAULT 10.0,
    MaxAttempts         INT DEFAULT 1,
    StartTime           DATETIME2 NOT NULL,
    EndTime             DATETIME2 NOT NULL,
    -- Cấu hình sinh đề
    GenerationMode      NVARCHAR(20) DEFAULT 'ByCount'
                        CHECK (GenerationMode IN ('ByCount','ByScore')),
    EasyCount           INT DEFAULT 0,
    MediumCount         INT DEFAULT 0,
    HardCount           INT DEFAULT 0,
    -- Bảo mật
    ShuffleQuestions    BIT DEFAULT 1,
    ShuffleAnswers      BIT DEFAULT 1,
    AntiCheat           BIT DEFAULT 1,
    AllowViewAnswer     BIT DEFAULT 0,
    AutoSubmit          BIT DEFAULT 1,
    -- Trạng thái
    Status              NVARCHAR(20) DEFAULT 'Draft'
                        CHECK (Status IN ('Draft','Published','Active','Closed')),
    CreatedAt           DATETIME2 DEFAULT GETDATE(),
    UpdatedAt           DATETIME2
);

CREATE TABLE ExamClasses (
    ExamId      INT NOT NULL REFERENCES Exams(Id),
    ClassId     INT NOT NULL REFERENCES Classes(Id),
    PRIMARY KEY (ExamId, ClassId)
    -- Một kỳ thi có thể giao cho nhiều lớp
);

CREATE TABLE ExamAttempts (
    Id              INT IDENTITY PRIMARY KEY,
    ExamId          INT NOT NULL REFERENCES Exams(Id),
    StudentId       INT NOT NULL REFERENCES Users(Id),
    AttemptNumber   INT DEFAULT 1,
    StartedAt       DATETIME2 NOT NULL DEFAULT GETDATE(),
    SubmittedAt     DATETIME2,
    ServerEndTime   DATETIME2 NOT NULL,          -- StartedAt + DurationMinutes — dùng để validate
    Status          NVARCHAR(20) DEFAULT 'InProgress'
                    CHECK (Status IN ('InProgress','Submitted','AutoSubmitted','Abandoned')),
    Score           FLOAT,
    TotalCorrect    INT,
    TotalWrong      INT,
    TotalSkipped    INT,
    Rank            INT,                         -- Xếp hạng trong kỳ thi
    TabSwitchCount  INT DEFAULT 0,               -- Anti-cheat: số lần chuyển tab
    IpAddress       NVARCHAR(50),
    CreatedAt       DATETIME2 DEFAULT GETDATE()
);

CREATE TABLE AttemptQuestions (
    Id              INT IDENTITY PRIMARY KEY,
    AttemptId       INT NOT NULL REFERENCES ExamAttempts(Id),
    QuestionId      INT NOT NULL REFERENCES Questions(Id),
    OrderIndex      INT NOT NULL,                -- Thứ tự câu hỏi trong đề (sau shuffle)
    AnswerSnapshot  NVARCHAR(MAX),               -- JSON snapshot đáp án tại thời điểm thi
    AnswerOrder     NVARCHAR(500)                -- JSON thứ tự đáp án (sau shuffle)
);

CREATE TABLE AttemptAnswers (
    Id              INT IDENTITY PRIMARY KEY,
    AttemptId       INT NOT NULL REFERENCES ExamAttempts(Id),
    QuestionId      INT NOT NULL REFERENCES Questions(Id),
    SelectedAnswerIds NVARCHAR(500),             -- JSON array answer IDs: '[1,3]'
    TextAnswer      NVARCHAR(2000),              -- Cho loại TextAnswer
    IsCorrect       BIT,
    PointsEarned    FLOAT DEFAULT 0,
    AnsweredAt      DATETIME2 DEFAULT GETDATE()
);
```

### Nhóm 5 — Học bạ & Báo cáo

```sql
CREATE TABLE AcademicRecords (
    Id              INT IDENTITY PRIMARY KEY,
    StudentId       INT NOT NULL REFERENCES Users(Id),
    SubjectId       INT NOT NULL REFERENCES Subjects(Id),
    SemesterId      INT NOT NULL REFERENCES Semesters(Id),
    AverageScore    FLOAT,
    Rank            NVARCHAR(20),               -- 'Xuất sắc','Giỏi','Khá','TB','Yếu'
    TotalAttempts   INT DEFAULT 0,
    BestScore       FLOAT,
    LastExamDate    DATETIME2,
    TeacherComment  NVARCHAR(2000),             -- Nhận xét của giáo viên
    UpdatedAt       DATETIME2
    -- Được cập nhật tự động sau mỗi lần nộp bài (trigger hoặc service)
);

CREATE TABLE Notifications (
    Id              INT IDENTITY PRIMARY KEY,
    OrganizationId  INT NOT NULL REFERENCES Organizations(Id),
    SenderId        INT REFERENCES Users(Id),
    Title           NVARCHAR(500) NOT NULL,
    Content         NVARCHAR(4000) NOT NULL,
    Type            NVARCHAR(30) NOT NULL
                    CHECK (Type IN ('Exam','Result','Urgent','System','Achievement')),
    ScheduledAt     DATETIME2,                  -- Hẹn giờ gửi
    SentAt          DATETIME2,
    CreatedAt       DATETIME2 DEFAULT GETDATE()
);

CREATE TABLE NotificationRecipients (
    Id              INT IDENTITY PRIMARY KEY,
    NotificationId  INT NOT NULL REFERENCES Notifications(Id),
    RecipientId     INT NOT NULL REFERENCES Users(Id),
    IsRead          BIT DEFAULT 0,
    ReadAt          DATETIME2
);
```

### Indexes quan trọng

```sql
-- Performance indexes
CREATE INDEX IX_Users_OrganizationId ON Users(OrganizationId);
CREATE INDEX IX_Users_Role ON Users(Role);
CREATE INDEX IX_Classes_AcademicYearId ON Classes(AcademicYearId);
CREATE INDEX IX_ClassStudents_StudentId ON ClassStudents(StudentId);
CREATE INDEX IX_Questions_SubjectId_Difficulty ON Questions(SubjectId, Difficulty) WHERE IsActive = 1;
CREATE INDEX IX_ExamAttempts_StudentId ON ExamAttempts(StudentId);
CREATE INDEX IX_ExamAttempts_ExamId_Status ON ExamAttempts(ExamId, Status);
CREATE INDEX IX_AttemptAnswers_AttemptId ON AttemptAnswers(AttemptId);
CREATE INDEX IX_AuditLogs_CreatedAt ON AuditLogs(CreatedAt DESC);
CREATE INDEX IX_UserSessions_UserId_IsRevoked ON UserSessions(UserId, IsRevoked);
```

---

## 3. SEED DATA

```sql
-- Organization
INSERT INTO Organizations (Name, Code, Address, Email, Website, Description, AntiCheatTabSwitch, ShuffleQuestions, ShuffleAnswers)
VALUES (N'THPT Chu Văn An', 'THPT-CVA-HN01', N'Đường Thụy Khuê, Tây Hồ, Hà Nội', 
        'contact@chuvan.edu.vn', 'https://thptchuvan.edu.vn', 
        N'Trường THPT Chu Văn An — Hà Nội. Đơn vị trực thuộc Sở GD&ĐT Hà Nội.', 1, 1, 1);

-- Academic Year
INSERT INTO AcademicYears (OrganizationId, Name, StartDate, EndDate, IsCurrent)
VALUES (1, '2024-2025', '2024-09-01', '2025-06-30', 1);

-- Semesters
INSERT INTO Semesters (AcademicYearId, Name, StartDate, EndDate, IsActive)
VALUES (1, N'Học kỳ 1', '2024-09-01', '2025-01-15', 0),
       (1, N'Học kỳ 2', '2025-01-20', '2025-06-30', 1);

-- Users (BCrypt hash cho 'password123')
DECLARE @hash NVARCHAR(255) = '$2a$12$...'; -- Thay bằng hash thực
INSERT INTO Users (OrganizationId, Username, PasswordHash, FullName, Email, Role) VALUES
(1, 'admin_root',    @hash, N'Admin Hệ Thống',    'admin@chuvan.edu.vn',   'Admin'),
(1, 'gv_tranbich',  @hash, N'Trần Thị Bích',     'bich.tt@chuvan.edu.vn', 'Teacher'),
(1, 'gv_vinhnt',    @hash, N'Nguyễn Thành Vinh', 'vinh.nt@chuvan.edu.vn', 'Teacher'),
(1, 'ts_thanhnt',   @hash, N'Nguyễn Thị Thanh',  'thanh.nt@chuvan.edu.vn','Student'),
(1, 'ts_hoangnm',   @hash, N'Phạm Minh Hoàng',   'hoang.pm@chuvan.edu.vn','Student');

-- Teacher Profiles
INSERT INTO TeacherProfiles (UserId, EmployeeCode, Specialization) VALUES
(2, 'GV-001', N'Toán học'), (3, 'GV-002', N'Vật lý');

-- Student Profiles
INSERT INTO StudentProfiles (UserId, StudentCode, DateOfBirth, Gender, ParentName, ParentPhone) VALUES
(4, 'HS-2024-0089', '2007-03-15', N'Nữ',  N'Nguyễn Văn Hùng', '0912345678'),
(5, 'HS-2024-0091', '2007-07-22', N'Nam', N'Phạm Văn Toàn',   '0987654321');

-- Classes
INSERT INTO Classes (OrganizationId, AcademicYearId, Name, Grade, Track, Room, HomeTeacherId) VALUES
(1, 1, '12A1', '12', N'Khoa học tự nhiên', N'Phòng 301', 2),
(1, 1, '12A2', '12', N'Khoa học tự nhiên', N'Phòng 302', 3);

-- Subjects (with ColorHex for calendar)
INSERT INTO Subjects (OrganizationId, Name, Code, ColorHex) VALUES
(1, N'Toán học',    'TOAN', '#185FA5'),
(1, N'Vật lý',      'VAT_LY', '#3B6D11'),
(1, N'Hóa học',     'HOA', '#854F0B'),
(1, N'Tiếng Anh',   'ANH', '#534AB7');

-- Questions (20 câu ví dụ — SingleChoice)
INSERT INTO Questions (SubjectId, CreatedByUserId, Type, Content, Difficulty) VALUES
(1, 2, 'SingleChoice', N'Phương trình x² - 5x + 6 = 0 có hai nghiệm là?', 'Easy'),
(1, 2, 'SingleChoice', N'Đạo hàm của hàm số f(x) = x³ - 3x² + 2 tại x=1 là?', 'Medium'),
(1, 2, 'TextAnswer',   N'Tính tích phân: ∫(2x+1)dx từ 0 đến 3', 'Hard');
-- ... (thêm 17 câu nữa)
```

---

## 4. PHÂN QUYỀN & AUTHENTICATION

### Roles & Permissions matrix

| Chức năng | Admin | Giáo viên | Học sinh |
|---|:---:|:---:|:---:|
| Quản lý tổ chức (cài đặt, năm học) | ✅ | ❌ | ❌ |
| Quản lý người dùng (CRUD) | ✅ | ❌ | ❌ |
| Quản lý lớp học | ✅ | Xem lớp mình | ❌ |
| Quản lý ngân hàng câu hỏi | ✅ | Môn mình | ❌ |
| Tạo kỳ thi | ✅ | ✅ | ❌ |
| Xem lịch thi | ✅ | ✅ | Lớp mình |
| Làm bài thi | ❌ | ❌ | ✅ |
| Xem hồ sơ học sinh | ✅ | Lớp mình | Bản thân |
| Xem học bạ | ✅ | Lớp mình | Bản thân |
| Báo cáo toàn trường | ✅ | Lớp mình | ❌ |
| Nhật ký kiểm toán | ✅ | ❌ | ❌ |
| Cài đặt tổ chức | ✅ | ❌ | ❌ |
| Phê duyệt câu hỏi chia sẻ | ✅ | ✅ | ❌ |
| Báo lỗi câu hỏi | ❌ | ❌ | ✅ |

### JWT Configuration

```csharp
// Program.cs
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => {
        options.TokenValidationParameters = new TokenValidationParameters {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"])),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

// Claims khi issue token:
// - "jti": Guid.NewGuid() — dùng để revoke trong UserSessions
// - "sub": userId
// - "role": "Admin" | "Teacher" | "Student"
// - "orgId": organizationId
// - "name": fullName
```

### Single-Session Middleware

```csharp
public class SingleSessionMiddleware : IMiddleware {
    public async Task InvokeAsync(HttpContext context, RequestDelegate next) {
        if (context.User.Identity?.IsAuthenticated == true) {
            var jti = context.User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
            var userId = int.Parse(context.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            
            // Kiểm tra session còn hiệu lực
            var session = await _sessionRepo.GetActiveSessionAsync(userId, jti);
            if (session == null || session.IsRevoked) {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Session expired or revoked.");
                return;
            }
        }
        await next(context);
    }
}

// Khi login mới: revoke tất cả session cũ của userId, tạo session mới
```

### Login Flow (tự detect role)

```csharp
// POST /api/auth/login
// Input: { username, password }
// Xử lý:
// 1. Tìm user theo username (không lộ thông tin nếu không tồn tại)
// 2. BCrypt.Verify(password, user.PasswordHash)
// 3. Nếu đúng → revoke session cũ, tạo JWT mới
// 4. Response: { token, role, fullName, orgId }
//    → Client redirect:
//       Admin    → /admin/dashboard
//       Teacher  → /teacher/dashboard  
//       Student  → /student/dashboard
```

---

## 5. GIAI ĐOẠN 1 — KHỞI TẠO & DATABASE

```
[ ] Khởi tạo ASP.NET Core 8 Web App (MVC + API controllers)
[ ] Cài packages: EFCore.SqlServer, BCrypt.Net-Next, Microsoft.AspNetCore.Authentication.JwtBearer,
                  Microsoft.AspNetCore.SignalR, MemoryCache, Rate-Limiting
[ ] DbContext với tất cả entities từ Section 2
[ ] Migrations: dotnet ef migrations add InitialCreate
[ ] Seed data từ Section 3
[ ] AsNoTracking() cho tất cả query READ-ONLY
[ ] IMemoryCache: cache danh sách Subjects (TTL 10 phút), câu hỏi theo SubjectId (TTL 5 phút)
[ ] Invalidate cache khi có CRUD trên Subjects/Questions
```

---

## 6. GIAI ĐOẠN 2 — BẢO MẬT CHUYÊN SÂU

```
[ ] BCrypt.Net — KHÔNG dùng MD5/SHA1/SHA256 plain
[ ] JWT Bearer với claims: jti, sub, role, orgId, name
[ ] Single-session middleware (xem Section 4)
[ ] Rate Limiting:
    - Login endpoint: 5 requests/phút/IP
    - API chung: 100 requests/phút/User
[ ] Content Security Policy (CSP) header middleware
[ ] AntiForgeryToken trên tất cả HTML Form
[ ] CORS policy: chỉ allow origin của chính app
[ ] AuditLog service: ghi log mọi action nhạy cảm
    - Login/Logout, đổi mật khẩu, khóa tài khoản
    - Xuất dữ liệu (học bạ PDF, Excel), xóa dữ liệu
    - Thêm/sửa/xóa user, câu hỏi, kỳ thi
[ ] Token revocation khi:
    - Admin khóa tài khoản
    - Người dùng đổi mật khẩu
    - Login từ thiết bị mới
```

---

## 7. GIAI ĐOẠN 3 — QUẢN LÝ TỔ CHỨC (ADMIN)

### 3A. Cài đặt tổ chức (`man_cai_dat_to_chuc.html`)

**Sidebar navigation (10 mục):**
- Thông tin trường — CRUD Organizations
- Năm học & Học kỳ — CRUD AcademicYears, Semesters
- Phân quyền — gán role, xem matrix
- Xác thực & Session — cấu hình session timeout, max sessions
- Chính sách mật khẩu — độ dài tối thiểu, yêu cầu ký tự đặc biệt
- Nhật ký kiểm toán — bảng AuditLogs, lọc theo user/action/date
- Thông báo & Email — bật/tắt email tự động
- Sao lưu & Khôi phục — trigger backup SQL
- Tích hợp (API) — quản lý API keys nếu cần

**Toggle features (6 cài đặt trong Organizations):**
```
AntiCheatTabSwitch | ShuffleQuestions | ShuffleAnswers
AutoSubmitOnTimeout | AllowViewAnswerAfterSubmit
ShareQuestionBank | SendEmailOnResult
```

**Vùng nguy hiểm:**
- Xóa toàn bộ dữ liệu kỳ thi cũ (theo năm học)
- Reset hệ thống — yêu cầu xác nhận 2 bước

### 3B. Quản lý người dùng

```
GET  /api/admin/users?role=&classId=&search=&page=&pageSize=
POST /api/admin/users                    — Tạo user
PUT  /api/admin/users/{id}              — Sửa user
PUT  /api/admin/users/{id}/toggle-active — Khóa/mở tài khoản
POST /api/admin/users/import            — Import Excel (mã HS, họ tên, lớp, ngày sinh)
GET  /api/admin/users/{id}/sessions     — Xem sessions đang hoạt động
DELETE /api/admin/users/{id}/sessions   — Force logout
```

### 3C. Quản lý lớp học (`screen_class_management_v2.html`)

```
GET  /api/admin/classes?yearId=&grade=&search=
POST /api/admin/classes                          — Tạo lớp mới
PUT  /api/admin/classes/{id}                     — Sửa thông tin lớp
GET  /api/admin/classes/{id}/students            — Danh sách học sinh
POST /api/admin/classes/{id}/students            — Thêm học sinh vào lớp
DELETE /api/admin/classes/{id}/students/{uid}    — Xóa học sinh khỏi lớp
POST /api/admin/classes/{id}/students/import     — Import Excel
GET  /api/admin/classes/{id}/teachers            — Giáo viên phụ trách
POST /api/admin/classes/{id}/teachers            — Gán giáo viên vào lớp
GET  /api/admin/classes/{id}/exams-upcoming      — Kỳ thi sắp tới của lớp
```

**Logic hiển thị status học sinh:**
- `Hoạt động`: dự thi ≥ 2 bài trong 2 tuần gần nhất
- `Ít hoạt động`: 1-3 tuần không thi
- `Cần theo dõi`: >3 tuần không thi HOẶC điểm TB < 5.0

---

## 8. GIAI ĐOẠN 4 — NGHIỆP VỤ GIÁO VIÊN

### 4A. Ngân hàng câu hỏi

```
GET  /api/teacher/questions?subjectId=&type=&difficulty=&tags=&search=&page=
POST /api/teacher/questions                    — Tạo câu hỏi (kèm answers)
PUT  /api/teacher/questions/{id}              — Sửa câu hỏi
DELETE /api/teacher/questions/{id}            — Xóa (soft delete: IsActive=0)
POST /api/teacher/questions/import            — Import từ Excel
GET  /api/teacher/questions/{id}/reports      — Xem báo lỗi từ học sinh
PUT  /api/teacher/questions/{id}/reports/{rid} — Xử lý báo lỗi (Fixed/Dismissed)
GET  /api/teacher/questions/weak              — Câu hỏi có CorrectRate < 40%
```

**Phê duyệt câu hỏi chia sẻ (ShareQuestionBank):**
```
GET  /api/teacher/questions/shared-pending    — Câu hỏi chờ duyệt từ GV khác
PUT  /api/teacher/questions/{id}/approve      — Duyệt câu hỏi chia sẻ
```

### 4B. Tạo kỳ thi (`screen_A_create_exam.html`)

**3 bước (wizard):**

**Bước 1 — Thông tin chung:**
- Tên kỳ thi, Môn học, Lớp áp dụng (1 lớp hoặc nhiều lớp — ExamClasses)
- Thời gian bắt đầu/kết thúc cửa sổ thi, Thời lượng làm bài
- Số lần thi tối đa

**Bước 2 — Cấu hình đề:**
- GenerationMode: ByCount | ByScore
- ByCount: chỉ định EasyCount/MediumCount/HardCount
- ByScore: chỉ định tổng điểm, hệ thống tính ngược số câu
- Xem trước: tổng câu, tỷ lệ khó, điểm/câu, tổng điểm
- Cảnh báo nếu ngân hàng câu hỏi không đủ
- Checkboxes bảo mật: ShuffleQuestions, ShuffleAnswers, AntiCheat, AutoSubmit, AllowViewAnswer

**Bước 3 — Phân công & Xem lại:**
- Xác nhận danh sách lớp được giao
- Preview toàn bộ cài đặt trước khi publish
- Lưu nháp (Status=Draft) hoặc Publish (Status=Published)

**Thuật toán sinh đề (KHÔNG ORDER BY NEWID() trực tiếp):**

```csharp
public List<int> GenerateExamQuestionIds(int subjectId, int easyCount, int mediumCount, int hardCount) {
    // Lấy danh sách questionIds theo difficulty từ cache
    var easyIds   = _cache.GetOrCreate($"q_easy_{subjectId}",   () => GetIds(subjectId, "Easy"));
    var mediumIds = _cache.GetOrCreate($"q_medium_{subjectId}", () => GetIds(subjectId, "Medium"));
    var hardIds   = _cache.GetOrCreate($"q_hard_{subjectId}",   () => GetIds(subjectId, "Hard"));
    
    // Fisher-Yates shuffle in-memory
    var result = new List<int>();
    result.AddRange(FisherYatesSample(easyIds,   easyCount));
    result.AddRange(FisherYatesSample(mediumIds, mediumCount));
    result.AddRange(FisherYatesSample(hardIds,   hardCount));
    
    // Shuffle kết quả cuối
    return FisherYatesShuffle(result);
}

private List<int> FisherYatesSample(List<int> source, int count) {
    var list = new List<int>(source);
    var rng = RandomNumberGenerator.Create(); // Crypto-safe
    for (int i = list.Count - 1; i > list.Count - count - 1; i--) {
        byte[] bytes = new byte[4];
        rng.GetBytes(bytes);
        int j = (int)(BitConverter.ToUInt32(bytes) % (uint)(i + 1));
        (list[i], list[j]) = (list[j], list[i]);
    }
    return list.TakeLast(count).ToList();
}
```

### 4C. Lịch thi (`screen_B_exam_calendar.html`)

```
GET /api/teacher/exams/calendar?month=&year=&classId=&subjectId=
    → Response: List<ExamCalendarItem> { examId, title, subjectColorHex, startTime, classNames[] }
GET /api/teacher/exams/upcoming?days=7
GET /api/teacher/exams/{id}/results          — Thống kê kết quả kỳ thi
GET /api/teacher/exams/{id}/results/export   — Xuất Excel điểm
```

**Validate không trùng lịch:** Khi tạo kỳ thi mới, kiểm tra xem các lớp được giao có kỳ thi nào cùng khung giờ không. Nếu có → cảnh báo (không block).

### 4D. Phân tích câu hỏi yếu

```
GET /api/teacher/analytics/weak-questions?subjectId=&threshold=40
    → Câu hỏi có CorrectRate < threshold%, sắp xếp tăng dần
GET /api/teacher/analytics/exam/{id}/question-stats
    → Từng câu: số người trả lời, % đúng, phân phối đáp án
```

---

## 9. GIAI ĐOẠN 5 — NGHIỆP VỤ HỌC SINH

### 5A. Trang làm bài thi (Focus Mode)

**Khởi động bài thi:**
```csharp
// POST /api/student/attempts/start
// Input: { examId }
// Validate:
// 1. Exam.Status == "Published" AND DateTime.UtcNow in [StartTime, EndTime]
// 2. ClassStudents: học sinh thuộc lớp được giao
// 3. AttemptCount < Exam.MaxAttempts
// Nếu đã có attempt InProgress cùng examId → return attempt cũ (resume)

// Response: {
//   attemptId, serverEndTime,       // Client dùng serverEndTime để đếm ngược
//   questions: [                    // Đề đã được shuffle
//     { id, type, content, answers: [{ id, content }] }   // Answers đã shuffle
//   ]
// }
```

**Đồng hồ đếm ngược đồng bộ server-time:**

```javascript
// Client JS
async function startTimer(serverEndTime) {
    const endMs = new Date(serverEndTime).getTime();
    
    async function syncWithServer() {
        const res = await fetch('/api/student/time');
        const { serverNow } = await res.json();
        const remaining = new Date(serverEndTime).getTime() - new Date(serverNow).getTime();
        return Math.max(0, remaining);
    }
    
    let remaining = await syncWithServer(); // Lấy lần đầu từ server
    
    const interval = setInterval(async () => {
        remaining -= 1000;
        updateTimerUI(remaining);
        
        // Re-sync mỗi 30 giây để tránh client hack
        if (remaining % 30000 === 0) {
            remaining = await syncWithServer();
        }
        
        if (remaining <= 0) {
            clearInterval(interval);
            autoSubmit();
        }
    }, 1000);
}
```

**Anti-cheat:**

```javascript
// Page Visibility API
document.addEventListener('visibilitychange', () => {
    if (document.hidden) {
        fetch('/api/student/attempts/tab-switch', { method: 'POST', 
              body: JSON.stringify({ attemptId }) });
        showWarning('Cảnh báo: Bạn đã rời khỏi trang thi! Hành động này đã được ghi lại.');
    }
});

// Chặn copy/paste
document.addEventListener('copy',  e => e.preventDefault());
document.addEventListener('paste', e => e.preventDefault());
document.addEventListener('contextmenu', e => e.preventDefault());

// Chặn DevTools (F12, Ctrl+Shift+I)
document.addEventListener('keydown', e => {
    if (e.key === 'F12' || (e.ctrlKey && e.shiftKey && e.key === 'I')) {
        e.preventDefault();
    }
});
```

**Auto-submit khi hết giờ:**

```csharp
// Server validate: nếu SubmittedAt > ServerEndTime → ghi là AutoSubmitted
// Chấp nhận submit nhưng đánh dấu để báo cáo
if (DateTime.UtcNow > attempt.ServerEndTime.AddSeconds(30)) {
    // 30 giây grace period cho network latency
    attempt.Status = "AutoSubmitted";
}
```

### 5B. Chấm điểm tự động

```csharp
public GradeResult GradeAttempt(ExamAttempt attempt) {
    float totalScore = 0;
    int correct = 0, wrong = 0, skipped = 0;
    float pointsPerQuestion = attempt.Exam.TotalScore / attempt.Exam.TotalQuestions;
    
    foreach (var answer in attempt.Answers) {
        var question = answer.Question;
        bool isCorrect = false;
        
        switch (question.Type) {
            case "SingleChoice":
            case "MultipleChoice":
                var correctIds = question.Answers.Where(a => a.IsCorrect).Select(a => a.Id).ToHashSet();
                var selectedIds = JsonSerializer.Deserialize<List<int>>(answer.SelectedAnswerIds ?? "[]").ToHashSet();
                isCorrect = correctIds.SetEquals(selectedIds);
                break;
            
            case "TextAnswer":
                // Normalize: lowercase, trim whitespace, bỏ dấu tiếng Việt (optional)
                var correctText = NormalizeText(question.Answers.First(a => a.IsCorrect).Content);
                var studentText = NormalizeText(answer.TextAnswer ?? "");
                isCorrect = correctText == studentText;
                break;
        }
        
        answer.IsCorrect = isCorrect;
        if (isCorrect) { correct++; totalScore += pointsPerQuestion; }
        else if (string.IsNullOrEmpty(answer.SelectedAnswerIds) && string.IsNullOrEmpty(answer.TextAnswer))
            skipped++;
        else wrong++;
    }
    
    // Cập nhật CorrectRate cho từng câu hỏi
    UpdateQuestionCorrectRates(attempt.Answers);
    
    // Cập nhật AcademicRecords
    UpdateAcademicRecord(attempt.StudentId, attempt.Exam.SubjectId, attempt.Exam.SemesterId);
    
    return new GradeResult { Score = Math.Round(totalScore, 2), Correct = correct, Wrong = wrong, Skipped = skipped };
}

private string NormalizeText(string input) =>
    input.Trim().ToLower()
         .Replace("  ", " ")
         .Normalize(NormalizationForm.FormD); // Unicode normalize
```

---

## 10. GIAI ĐOẠN 6 — MỞ RỘNG V2

### 6A. Hồ sơ học sinh (`screen_student_profile_v2.html`)

**4 tab:**
1. **Tổng quan:** stat cards (điểm TB, bài đã thi, tỷ lệ đạt, xếp hạng lớp), biểu đồ cột điểm theo tháng, bảng điểm theo môn với progress bar, so sánh với trung bình lớp
2. **Lịch sử thi:** bảng tất cả ExamAttempts, lọc theo môn/kỳ/kết quả, click vào xem chi tiết từng câu
3. **Cài đặt:** đổi mật khẩu, cập nhật email/SĐT

**API:**
```
GET /api/student/profile/{id}          — Thông tin cá nhân + stats
GET /api/student/profile/{id}/progress — Điểm theo tháng (6 tháng gần nhất)
GET /api/student/profile/{id}/subjects — Điểm TB theo từng môn
GET /api/student/profile/{id}/history  — Lịch sử thi phân trang
```

### 6B. Học bạ điện tử (`man_hoc_ba_dien_tu.html`)

**Tính năng cốt lõi:**
- Xem học bạ theo học kỳ (HK1, HK2, Cả năm) — đọc từ AcademicRecords
- Bảng điểm từng môn: điểm HK1, HK2, Cả năm, Xếp loại
- Nhận xét của GVCN (TeacherComment trong AcademicRecords)
- Xếp loại tự động theo ngưỡng: Xuất sắc (≥9), Giỏi (≥8), Khá (≥6.5), TB (≥5), Yếu (<5)
- **Xuất PDF học bạ** — dùng template HTML → headless browser hoặc QuestPDF

**API:**
```
GET  /api/teacher/academic-records?studentId=&semesterId=
PUT  /api/teacher/academic-records/{studentId}/{subjectId}/{semesterId}/comment
GET  /api/teacher/academic-records/{studentId}/export-pdf
GET  /api/teacher/academic-records/class/{classId}/export-excel — Xuất cả lớp
```

**Logic cập nhật AcademicRecords:**
- Trigger tự động sau mỗi lần GradeAttempt
- Service `AcademicRecordService.Recalculate(studentId, subjectId, semesterId)` tính lại từ tất cả AttemptAnswers

### 6C. Hồ sơ giáo viên

```
GET  /api/admin/teachers/{id}/profile
PUT  /api/admin/teachers/{id}/profile
GET  /api/admin/teachers/{id}/stats         — Số câu hỏi đã tạo, số kỳ thi, lớp phụ trách
GET  /api/admin/teachers/{id}/question-bank — Ngân hàng câu hỏi của GV
```

### 6D. Thông báo & Giao tiếp (`screen_notifications_v2.html`)

**Phân loại:** Exam | Result | Urgent | System | Achievement

**Gửi thông báo:**
```
POST /api/notifications/send
Body: {
    targetType: "all" | "grade" | "class" | "students",
    targetIds: [classId1, classId2] | null,
    title, content, type,
    scheduledAt: null | ISO8601   — hẹn giờ gửi
}
```

**Thông báo tự động (Background Service — IHostedService):**
- Nhắc thi: 24h và 1h trước kỳ thi → gửi cho học sinh thuộc lớp
- Kết quả có: ngay sau khi GradeAttempt → gửi cho học sinh
- Cảnh báo học sinh cần theo dõi: kiểm tra hàng ngày lúc 7:00 sáng
  - Điều kiện: >3 tuần không thi HOẶC điểm TB < 5.0 HOẶC bỏ 3 bài liên tiếp
  - Gửi cho GVCN lớp
- Ngân hàng câu hỏi thấp: khi Questions đếm theo subjectId/difficulty < ngưỡng (VD: 20 câu)

**Đánh dấu đã đọc:**
```
PUT /api/notifications/{id}/read
PUT /api/notifications/read-all
```

### 6E. Báo lỗi câu hỏi (Question Reports)

```
POST /api/student/questions/{id}/report   — Học sinh báo lỗi
    Body: { reason: "Câu hỏi sai đáp án | Nội dung không rõ | Khác" }

GET  /api/teacher/questions/reports?status=Pending
PUT  /api/teacher/questions/reports/{id}  — Xử lý: Fixed | Dismissed
    Body: { status, teacherNote, fixedContent? }
```

### 6F. Năm học & Học kỳ

```
GET  /api/admin/academic-years
POST /api/admin/academic-years
PUT  /api/admin/academic-years/{id}/set-current  — Đổi năm học hiện tại
GET  /api/admin/semesters?yearId=
POST /api/admin/semesters
PUT  /api/admin/semesters/{id}/set-active
```

---

## 11. GIAI ĐOẠN 7 — PHÂN TÍCH & BÁO CÁO

### Màn hình `man_analytics_report.html`

**Sidebar 9 mục:**

| Mục | Dữ liệu | API |
|---|---|---|
| Tổng quan | stat cards + bar chart theo tháng + phân phối điểm | `/api/analytics/overview?semesterId=` |
| Theo lớp | So sánh điểm TB giữa các lớp | `/api/analytics/classes?semesterId=` |
| Theo học sinh | Bảng học sinh + điểm, lọc/sort | `/api/analytics/students?classId=&semesterId=` |
| Theo kỳ thi | Thống kê từng kỳ thi: TB, phân phối, câu yếu | `/api/analytics/exams/{id}` |
| Tiến độ học tập | Line chart điểm theo thời gian từng học sinh | `/api/analytics/students/{id}/trend` |
| Câu hỏi yếu | Danh sách câu CorrectRate < 40% | `/api/analytics/weak-questions?subjectId=` |
| So sánh lớp | Bar chart ngang nhiều lớp nhiều môn | `/api/analytics/classes/compare` |
| Xuất Excel | Toàn bộ dữ liệu theo bộ lọc | `/api/analytics/export/excel` |
| Xuất PDF học bạ | Hàng loạt cho cả lớp | `/api/analytics/export/report-cards?classId=` |

**Phân phối điểm (tính từ AttemptAnswers):**
```sql
SELECT 
    CASE 
        WHEN Score >= 9  THEN N'Xuất sắc'
        WHEN Score >= 8  THEN N'Giỏi'
        WHEN Score >= 6.5 THEN N'Khá'
        WHEN Score >= 5  THEN N'Trung bình'
        ELSE N'Yếu'
    END AS Grade,
    COUNT(*) AS Count,
    CAST(COUNT(*) * 100.0 / SUM(COUNT(*)) OVER() AS INT) AS Percent
FROM ExamAttempts
WHERE ExamId IN (SELECT Id FROM Exams WHERE SemesterId = @semesterId)
      AND Status IN ('Submitted','AutoSubmitted')
GROUP BY CASE 
    WHEN Score >= 9  THEN N'Xuất sắc'
    ...
END
```

**Xuất báo cáo:**
- Excel: dùng ClosedXML hoặc EPPlus
- PDF học bạ: dùng QuestPDF hoặc template Razor → wkhtmltopdf
- Có nút "Gửi báo cáo cho Ban giám hiệu" → tạo Notification type=System gửi cho Admin

**Caching cho analytics:**
```csharp
// Cache analytics nặng 30 phút, invalidate khi có kỳ thi mới kết thúc
_cache.Set($"analytics_overview_{orgId}_{semesterId}", result, TimeSpan.FromMinutes(30));
```

---

## 12. GIAI ĐOẠN 8 — DEPLOY

### Dockerfile (multi-stage)

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app
COPY *.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/out .
EXPOSE 8080
ENTRYPOINT ["dotnet", "QuizArena.dll"]
```

### docker-compose.yml

```yaml
version: '3.8'
services:
  web:
    build: .
    ports: ["8080:8080"]
    environment:
      - ConnectionStrings__Default=${DB_CONNECTION_STRING}
      - Jwt__Secret=${JWT_SECRET}
      - Jwt__Issuer=QuizArena
      - Jwt__Audience=QuizArenaUsers
    depends_on: [db]
  
  db:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      SA_PASSWORD: ${SA_PASSWORD}
      ACCEPT_EULA: "Y"
    volumes: ["sqldata:/var/opt/mssql"]

volumes:
  sqldata:
```

### appsettings.json (không hardcode secrets)

```json
{
  "ConnectionStrings": {
    "Default": ""
  },
  "Jwt": {
    "Secret": "",
    "Issuer": "QuizArena",
    "Audience": "QuizArenaUsers",
    "ExpirationHours": 8
  },
  "RateLimit": {
    "LoginPerMinute": 5,
    "ApiPerMinute": 100
  },
  "Cache": {
    "SubjectsTTLMinutes": 10,
    "QuestionsTTLMinutes": 5,
    "AnalyticsTTLMinutes": 30
  }
}
```

### Hosting options

| Thành phần | Free tier | Paid |
|---|---|---|
| Database | Azure SQL Free (32GB) hoặc Somee | Azure SQL Basic |
| Web App | Render.com (free tier) | Azure App Service B1 |
| File storage | Cloudflare R2 free 10GB | Azure Blob |

---

## 13. API ENDPOINTS ĐẦY ĐỦ

### Auth
```
POST /api/auth/login               — Đăng nhập, tự detect role, trả về JWT
POST /api/auth/logout              — Revoke session
POST /api/auth/refresh             — Refresh JWT (nếu implement refresh token)
POST /api/auth/change-password     — Đổi mật khẩu
GET  /api/student/time             — Lấy server time (dùng cho timer đồng bộ)
```

### Admin
```
GET|POST          /api/admin/users
GET|PUT           /api/admin/users/{id}
PUT               /api/admin/users/{id}/toggle-active
POST              /api/admin/users/import
DELETE            /api/admin/users/{id}/sessions

GET|POST          /api/admin/classes
GET|PUT           /api/admin/classes/{id}
GET|POST|DELETE   /api/admin/classes/{id}/students
POST              /api/admin/classes/{id}/students/import
GET               /api/admin/classes/{id}/teachers

GET|PUT           /api/admin/organization
GET               /api/admin/audit-logs?action=&userId=&from=&to=&page=

GET|POST          /api/admin/academic-years
PUT               /api/admin/academic-years/{id}/set-current
GET|POST          /api/admin/semesters
PUT               /api/admin/semesters/{id}/set-active
```

### Teacher
```
GET|POST          /api/teacher/questions
GET|PUT|DELETE    /api/teacher/questions/{id}
POST              /api/teacher/questions/import
GET               /api/teacher/questions/weak
GET               /api/teacher/questions/shared-pending
PUT               /api/teacher/questions/{id}/approve
GET               /api/teacher/questions/{id}/reports
PUT               /api/teacher/questions/reports/{id}

GET|POST          /api/teacher/exams
GET|PUT|DELETE    /api/teacher/exams/{id}
PUT               /api/teacher/exams/{id}/publish
GET               /api/teacher/exams/calendar?month=&year=
GET               /api/teacher/exams/{id}/results
GET               /api/teacher/exams/{id}/results/export

GET               /api/teacher/academic-records?studentId=&semesterId=
PUT               /api/teacher/academic-records/{studentId}/{subjectId}/{semesterId}/comment
GET               /api/teacher/academic-records/{studentId}/export-pdf
GET               /api/teacher/academic-records/class/{classId}/export-excel

GET|PUT           /api/teacher/profile
GET               /api/teacher/analytics/overview?semesterId=
GET               /api/teacher/analytics/classes?semesterId=
GET               /api/teacher/analytics/students?classId=
GET               /api/teacher/analytics/exams/{id}
GET               /api/teacher/analytics/weak-questions?subjectId=
GET               /api/teacher/analytics/export/excel
```

### Student
```
GET               /api/student/dashboard          — Danh sách kỳ thi đang mở
GET               /api/student/exams/{id}          — Chi tiết kỳ thi
POST              /api/student/attempts/start      — Bắt đầu thi
PUT               /api/student/attempts/{id}/answer — Lưu đáp án realtime
POST              /api/student/attempts/{id}/submit — Nộp bài
POST              /api/student/attempts/tab-switch  — Ghi log chuyển tab
GET               /api/student/attempts/{id}/result — Xem kết quả (nếu AllowViewAnswer)

GET               /api/student/profile             — Hồ sơ cá nhân
GET               /api/student/profile/progress    — Điểm theo tháng
GET               /api/student/profile/subjects    — Điểm theo môn
GET               /api/student/profile/history     — Lịch sử thi

POST              /api/student/questions/{id}/report — Báo lỗi câu hỏi

GET               /api/student/time                — Server time cho timer
```

### Notifications
```
GET               /api/notifications?type=&isRead=&page=
POST              /api/notifications/send           — Admin/Teacher gửi
PUT               /api/notifications/{id}/read
PUT               /api/notifications/read-all
GET               /api/notifications/unread-count
```

### Subjects
```
GET               /api/subjects                    — Cached, dùng cho dropdowns
POST              /api/admin/subjects
PUT               /api/admin/subjects/{id}
```

---

## 14. CHECKLIST NGHIỆM THU

### Bảo mật (phải pass 100%)
```
[ ] Mật khẩu hash BCrypt — tuyệt đối không lưu plain text
[ ] JWT có jti, validate trong DB với UserSessions
[ ] Single-session: login mới → revoke session cũ
[ ] Rate limiting: login 5/phút/IP, API 100/phút/user
[ ] CSP header chặn inline script và external origins
[ ] AntiForgeryToken trên tất cả POST form
[ ] SQL injection: 100% parameterized query qua EF Core
[ ] Authorization: mọi endpoint có [Authorize(Roles=...)]
[ ] AuditLog: 6 loại action nhạy cảm được ghi đầy đủ
[ ] HTTPS enforced (UseHttpsRedirection + HSTS)
```

### Tính năng cốt lõi
```
[ ] Đăng nhập → tự redirect đúng dashboard theo role
[ ] Admin CRUD: Users, Classes, Organization settings
[ ] Import học sinh từ Excel (mẫu file có sẵn)
[ ] Giáo viên CRUD câu hỏi (3 loại) + import Excel
[ ] Tạo kỳ thi 3 bước, sinh đề Fisher-Yates
[ ] Lịch thi calendar view (tháng/tuần/danh sách)
[ ] Học sinh làm bài: timer đồng bộ server, grid câu hỏi
[ ] Anti-cheat: tab switch warning + ghi log
[ ] Nộp bài: chấm điểm tự động (3 loại câu)
[ ] TextAnswer normalize: lowercase + trim + unicode
[ ] Auto-submit khi hết giờ (server validate)
[ ] Trang kết quả: đúng/sai từng câu, điểm, xếp hạng
```

### Tính năng V2 (mở rộng)
```
[ ] Hồ sơ học sinh: biểu đồ tiến bộ, so sánh với lớp
[ ] Học bạ điện tử: điểm từng môn/HK, nhận xét GVCN, xuất PDF
[ ] Hồ sơ giáo viên: thông tin, thống kê
[ ] Thông báo: gửi theo nhóm, hẹn giờ, đánh dấu đã đọc
[ ] Thông báo tự động: nhắc thi, kết quả, cảnh báo học sinh
[ ] Báo lỗi câu hỏi (học sinh) + xử lý (giáo viên)
[ ] Báo cáo & Phân tích: phân phối điểm, câu hỏi yếu, so sánh lớp
[ ] Xuất Excel (điểm lớp) + xuất PDF học bạ hàng loạt
[ ] Cài đặt tổ chức: 8 toggle tính năng + nhật ký kiểm toán
[ ] Năm học & Học kỳ: quản lý, đổi kỳ hiện tại
[ ] AcademicRecords: tự động cập nhật sau mỗi lần chấm
```

### Hiệu năng
```
[ ] AsNoTracking() trên tất cả query read-only
[ ] Index: 8 indexes quan trọng đã tạo
[ ] IMemoryCache: Subjects (10 phút), Questions by subject (5 phút), Analytics (30 phút)
[ ] Phân trang: tất cả danh sách dài đều có ?page=&pageSize=
[ ] Fisher-Yates in-memory — không ORDER BY NEWID() trên bảng lớn
[ ] Lazy loading tắt, dùng explicit Include() có chọn lọc
```

---

## GHI CHÚ CHO AI AGENT

**Thứ tự build khuyến nghị:**
1. Database migrations + Seed data
2. Auth (JWT, login, single-session, rate-limit)
3. Admin: Users CRUD, Classes, Organization
4. Teacher: Questions CRUD, Exam CRUD, Calendar
5. Student: Làm bài, chấm điểm, xem kết quả
6. V2: Profile, Học bạ, Notifications, Analytics
7. Export PDF/Excel
8. Background services (auto-notify)
9. Docker + deploy

**UI style:** Tham chiếu 11 file HTML mockup đã thiết kế. Design system: Anthropic CSS variables (`--color-text-primary`, `--color-background-secondary`, v.v.), màu brand `#185FA5` (blue), Tabler icons outline, font sans-serif, border 0.5px, border-radius 8-12px, flat design không shadow.

**Khi gặp ambiguity:** Ưu tiên bảo mật và UX đơn giản hơn feature phức tạp. Nếu feature V2 xung đột với deadline, drop xuống Backlog thay vì implement nửa vời.
