# ExamHub — Đặc tả kỹ thuật đầy đủ cho AI Agent

> Tài liệu này là nguồn sự thật duy nhất (single source of truth) để AI agent sinh code.  
> Đọc toàn bộ trước khi bắt đầu bất kỳ file nào.

---

## 0. Tổng quan dự án

| Mục | Giá trị |
|-----|---------|
| Tên hệ thống | ExamHub — Website Thi Trắc Nghiệm Trực Tuyến |
| Nền tảng | ASP.NET Core 8 MVC + Web API |
| ORM | Entity Framework Core 8 |
| Database | MS SQL Server 2019+ |
| Auth | ASP.NET Core Identity + JWT Bearer |
| Cache | IMemoryCache (in-process) |
| Container | Docker (multi-stage build) |
| Triết lý UI | Minimalist Modern — flat, trắng, không shadow, responsive |

---

## 1. Kiến trúc solution

```
ExamHub.sln
├── ExamHub.Web          # ASP.NET Core MVC (Views, Controllers)
├── ExamHub.Api          # ASP.NET Core Web API (endpoints cho JS client)
├── ExamHub.Core         # Domain entities, interfaces, enums
├── ExamHub.Infrastructure  # EF Core DbContext, Repositories, Migrations
├── ExamHub.Tests        # xUnit unit tests
└── docker-compose.yml
```

Các layer giao tiếp theo hướng: `Web/Api → Core ← Infrastructure`.  
Không được để `Infrastructure` import `Web` hoặc `Api`.

---

## 2. Database schema

### 2.1 Bảng `Users`

```sql
CREATE TABLE Users (
    Id          UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    Username    NVARCHAR(50)  NOT NULL UNIQUE,
    Email       NVARCHAR(150) NOT NULL UNIQUE,
    FullName    NVARCHAR(100) NOT NULL,
    PasswordHash NVARCHAR(255) NOT NULL,   -- BCrypt / Argon2id, KHÔNG dùng MD5/SHA1
    Role        NVARCHAR(20)  NOT NULL CHECK (Role IN ('Admin','Teacher','Student')),
    IsActive    BIT           NOT NULL DEFAULT 1,
    CreatedAt   DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
    LastLoginAt DATETIME2     NULL
);
```

### 2.2 Bảng `Subjects`

```sql
CREATE TABLE Subjects (
    Id          INT IDENTITY PRIMARY KEY,
    Name        NVARCHAR(100) NOT NULL,
    Description NVARCHAR(500) NULL,
    CreatedBy   UNIQUEIDENTIFIER NOT NULL REFERENCES Users(Id),
    IsActive    BIT NOT NULL DEFAULT 1,
    CreatedAt   DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);
```

### 2.3 Bảng `Questions`

```sql
CREATE TABLE Questions (
    Id          INT IDENTITY PRIMARY KEY,
    SubjectId   INT NOT NULL REFERENCES Subjects(Id),
    Content     NVARCHAR(MAX) NOT NULL,
    Type        NVARCHAR(20) NOT NULL CHECK (Type IN ('SingleChoice','MultipleChoice','TextAnswer')),
    Difficulty  NVARCHAR(10) NOT NULL CHECK (Difficulty IN ('Easy','Medium','Hard')),
    Points      DECIMAL(4,2) NOT NULL DEFAULT 1.0,
    IsActive    BIT NOT NULL DEFAULT 1,
    CreatedBy   UNIQUEIDENTIFIER NOT NULL REFERENCES Users(Id),
    CreatedAt   DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);
```

### 2.4 Bảng `Answers`

```sql
CREATE TABLE Answers (
    Id          INT IDENTITY PRIMARY KEY,
    QuestionId  INT NOT NULL REFERENCES Questions(Id) ON DELETE CASCADE,
    Content     NVARCHAR(MAX) NOT NULL,
    IsCorrect   BIT NOT NULL DEFAULT 0,
    OrderIndex  TINYINT NOT NULL DEFAULT 0
);
-- Với câu TextAnswer: IsCorrect=1, Content = đáp án chuẩn (có thể nhiều hàng = nhiều đáp án chấp nhận)
```

### 2.5 Bảng `Exams`

```sql
CREATE TABLE Exams (
    Id              INT IDENTITY PRIMARY KEY,
    SubjectId       INT NOT NULL REFERENCES Subjects(Id),
    Title           NVARCHAR(200) NOT NULL,
    Description     NVARCHAR(500) NULL,
    DurationMinutes INT NOT NULL,
    GenerateMode    NVARCHAR(20) NOT NULL CHECK (GenerateMode IN ('ByCount','ByPoints')),
    QuestionCount   INT NULL,       -- dùng khi GenerateMode = 'ByCount'
    TotalPoints     DECIMAL(6,2) NULL, -- dùng khi GenerateMode = 'ByPoints'
    StartTime       DATETIME2 NOT NULL,
    EndTime         DATETIME2 NOT NULL,
    IsActive        BIT NOT NULL DEFAULT 1,
    CreatedBy       UNIQUEIDENTIFIER NOT NULL REFERENCES Users(Id),
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);
```

### 2.6 Bảng `ExamAttempts`

```sql
CREATE TABLE ExamAttempts (
    Id          UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    ExamId      INT NOT NULL REFERENCES Exams(Id),
    UserId      UNIQUEIDENTIFIER NOT NULL REFERENCES Users(Id),
    StartedAt   DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    SubmittedAt DATETIME2 NULL,
    Score       DECIMAL(5,2) NULL,
    TotalPoints DECIMAL(5,2) NULL,
    IpAddress   NVARCHAR(50) NULL,
    Status      NVARCHAR(20) NOT NULL DEFAULT 'InProgress'
                CHECK (Status IN ('InProgress','Submitted','TimedOut','Flagged')),
    UNIQUE (ExamId, UserId)   -- mỗi thí sinh chỉ thi một lần
);
```

### 2.7 Bảng `ExamAttemptAnswers`

```sql
CREATE TABLE ExamAttemptAnswers (
    Id          INT IDENTITY PRIMARY KEY,
    AttemptId   UNIQUEIDENTIFIER NOT NULL REFERENCES ExamAttempts(Id) ON DELETE CASCADE,
    QuestionId  INT NOT NULL REFERENCES Questions(Id),
    AnswerIds   NVARCHAR(MAX) NULL,   -- JSON array of Answer.Id (SingleChoice / MultipleChoice)
    TextInput   NVARCHAR(500) NULL,   -- dùng với TextAnswer
    IsCorrect   BIT NULL,
    PointsEarned DECIMAL(4,2) NULL
);
```

### 2.8 Bảng `ExamQuestionSnapshot`

```sql
-- Lưu đề thi đã bốc thăm cho từng attempt (không thay đổi dù ngân hàng cập nhật)
CREATE TABLE ExamQuestionSnapshot (
    AttemptId   UNIQUEIDENTIFIER NOT NULL REFERENCES ExamAttempts(Id) ON DELETE CASCADE,
    QuestionId  INT NOT NULL REFERENCES Questions(Id),
    OrderIndex  TINYINT NOT NULL,
    PRIMARY KEY (AttemptId, QuestionId)
);
```

### 2.9 Bảng `ActiveSessions`

```sql
-- Dùng cho middleware 1 session / tài khoản
CREATE TABLE ActiveSessions (
    UserId      UNIQUEIDENTIFIER NOT NULL REFERENCES Users(Id),
    TokenHash   NVARCHAR(255) NOT NULL,
    CreatedAt   DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    ExpiresAt   DATETIME2 NOT NULL,
    PRIMARY KEY (UserId)   -- chỉ một session tại một thời điểm
);
```

---

## 3. Seed data bắt buộc

File: `Infrastructure/Data/SeedData.cs`

```
1 Admin:    username=admin_root,   password=Admin@123456,  role=Admin
1 Teacher:  username=gv_tranvan,   password=Teacher@123,  role=Teacher, subject=Đại số tuyến tính
2 Students: username=ts_nguyen01,  password=Student@123,  role=Student
            username=ts_lehoang,   password=Student@123,  role=Student

2 Subjects: "Đại số tuyến tính", "Lập trình Web"

20 Questions cho "Đại số tuyến tính":
  - 10 câu SingleChoice (5 Easy, 5 Medium)
  - 5 câu MultipleChoice (Medium/Hard)
  - 5 câu TextAnswer (Easy)

1 Exam: "Đại số — Giữa kỳ 2026", Duration=60min, GenerateMode=ByCount, QuestionCount=10,
        StartTime=now, EndTime=now+7days
```

---

## 4. Bảo mật — yêu cầu bắt buộc

### 4.1 Password hashing

```csharp
// Dùng BCrypt.Net-Next (NuGet: BCrypt.Net-Next)
string hash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
bool valid = BCrypt.Net.BCrypt.Verify(password, hash);
// TUYỆT ĐỐI không dùng MD5, SHA1, SHA256 để hash mật khẩu
```

### 4.2 JWT configuration

```csharp
// appsettings.json — đọc từ environment variable
"Jwt": {
  "Secret": "",        // đọc từ env JWT_SECRET (min 32 chars)
  "Issuer": "ExamHub",
  "Audience": "ExamHubUsers",
  "ExpiryMinutes": 60
}
```

Khi token hết hạn hoặc bị revoke (row trong `ActiveSessions` bị xóa), trả 401.

### 4.3 Rate Limiting (ASP.NET Core 8 built-in)

```csharp
// Program.cs
builder.Services.AddRateLimiter(opt => {
    opt.AddFixedWindowLimiter("login", cfg => {
        cfg.PermitLimit = 5;
        cfg.Window = TimeSpan.FromMinutes(1);
        cfg.QueueLimit = 0;
    });
    opt.AddFixedWindowLimiter("api", cfg => {
        cfg.PermitLimit = 60;
        cfg.Window = TimeSpan.FromMinutes(1);
    });
});
// Áp dụng "login" cho POST /Account/Login và POST /api/auth/login
// Áp dụng "api" cho toàn bộ /api/*
```

### 4.4 Anti-CSRF

```csharp
// Trên mọi form POST trong MVC Views:
@Html.AntiForgeryToken()
// Trên Controller action:
[ValidateAntiForgeryToken]
```

Với API endpoint dùng JWT thì không cần AntiForgeryToken.

### 4.5 Content Security Policy

```csharp
// Middleware trong Program.cs
app.Use(async (ctx, next) => {
    ctx.Response.Headers.Append("Content-Security-Policy",
        "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:");
    await next();
});
```

### 4.6 Single-session middleware

```csharp
// Middleware tự viết: SingleSessionMiddleware.cs
// Logic:
// 1. Lấy UserId từ JWT claims
// 2. Query ActiveSessions WHERE UserId = @uid
// 3. So sánh TokenHash(request_token) == stored TokenHash
// 4. Nếu không khớp → trả 401 "Session expired on another device"
// 5. Cập nhật ExpiresAt mỗi request hợp lệ
```

### 4.7 Input validation & SQL Injection

- Toàn bộ query qua EF Core LINQ — không dùng raw SQL nếu không có `FromSqlInterpolated`.
- Validate tất cả input bằng Data Annotations + FluentValidation.
- Encode output HTML bằng Razor `@variable` (tự encode), không dùng `@Html.Raw`.

---

## 5. Thuật toán sinh đề ngẫu nhiên

```csharp
// Không dùng ORDER BY NEWID() trực tiếp trên bảng lớn
// Dùng thuật toán Fisher-Yates shuffle trong application layer:

public async Task<List<Question>> DrawQuestions(int subjectId, int count)
{
    // 1. Lấy danh sách Id câu hỏi (chỉ select Id, không load toàn bộ)
    var ids = await _db.Questions
        .AsNoTracking()
        .Where(q => q.SubjectId == subjectId && q.IsActive)
        .Select(q => q.Id)
        .ToListAsync();

    // 2. Fisher-Yates shuffle
    var rng = RandomNumberGenerator.Create();
    for (int i = ids.Count - 1; i > 0; i--) {
        int j = GetSecureRandom(rng, i + 1);
        (ids[i], ids[j]) = (ids[j], ids[i]);
    }

    // 3. Lấy 'count' Id đầu tiên, rồi load đầy đủ dữ liệu
    var selectedIds = ids.Take(count).ToList();
    return await _db.Questions
        .AsNoTracking()
        .Include(q => q.Answers)
        .Where(q => selectedIds.Contains(q.Id))
        .ToListAsync();
}

// Với GenerateMode = ByPoints: chạy tương tự, dừng khi tổng Points >= target
```

---

## 6. Đồng bộ thời gian thi (chống hack timer)

```csharp
// API endpoint: GET /api/exam/time-remaining/{attemptId}
// Trả về số giây còn lại tính từ Server (không tin client)

[HttpGet("time-remaining/{attemptId}")]
[Authorize(Roles = "Student")]
public async Task<IActionResult> GetTimeRemaining(Guid attemptId)
{
    var attempt = await _db.ExamAttempts
        .AsNoTracking()
        .Include(a => a.Exam)
        .FirstOrDefaultAsync(a => a.Id == attemptId && a.UserId == CurrentUserId);

    if (attempt == null) return NotFound();
    if (attempt.Status != "InProgress") return BadRequest("Attempt not in progress");

    var deadline = attempt.StartedAt.AddMinutes(attempt.Exam.DurationMinutes);
    var remaining = (int)(deadline - DateTime.UtcNow).TotalSeconds;

    if (remaining <= 0) {
        // Tự động nộp bài
        await AutoSubmit(attemptId);
        return Ok(new { remaining = 0, autoSubmitted = true });
    }

    return Ok(new { remaining });
}
```

JavaScript client poll endpoint này mỗi 30 giây để sync:

```javascript
// exam.js
async function syncTimer() {
    const res = await fetch(`/api/exam/time-remaining/${attemptId}`);
    const data = await res.json();
    if (data.autoSubmitted) { window.location.href = '/exam/result/' + attemptId; return; }
    remainingSeconds = data.remaining;
}
setInterval(syncTimer, 30000);
```

---

## 7. Anti-cheat

```javascript
// exam.js — chạy trong trang làm bài

// 7.1 Tab switch detection
let tabSwitchCount = 0;
document.addEventListener('visibilitychange', () => {
    if (document.hidden) {
        tabSwitchCount++;
        fetch('/api/exam/flag', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'Authorization': 'Bearer ' + token },
            body: JSON.stringify({ attemptId, reason: 'tab_switch', count: tabSwitchCount })
        });
        showWarning(`Cảnh báo ${tabSwitchCount}/3: Không được chuyển tab!`);
        if (tabSwitchCount >= 3) autoSubmitAndRedirect();
    }
});

// 7.2 Block copy/paste
document.addEventListener('copy', e => e.preventDefault());
document.addEventListener('paste', e => e.preventDefault());
document.addEventListener('contextmenu', e => e.preventDefault());

// 7.3 Block F12 / DevTools shortcut (best-effort, không tuyệt đối)
document.addEventListener('keydown', e => {
    if (e.key === 'F12' || (e.ctrlKey && e.shiftKey && e.key === 'I')) e.preventDefault();
});
```

API endpoint nhận flag:

```csharp
[HttpPost("flag")]
[Authorize(Roles = "Student")]
public async Task<IActionResult> FlagAttempt([FromBody] FlagRequest req)
{
    // Lưu vào cột Notes của ExamAttempts hoặc bảng AuditLog
    // Nếu tab_switch >= 3 → set Status = 'Flagged', tự nộp
}
```

---

## 8. Chấm điểm tự động

```csharp
public class GradingService
{
    public GradeResult GradeAnswer(Question question, ExamAttemptAnswer studentAnswer)
    {
        return question.Type switch {
            "SingleChoice" => GradeSingle(question, studentAnswer),
            "MultipleChoice" => GradeMultiple(question, studentAnswer),
            "TextAnswer" => GradeText(question, studentAnswer),
            _ => new GradeResult { IsCorrect = false, Points = 0 }
        };
    }

    private GradeResult GradeText(Question q, ExamAttemptAnswer a)
    {
        // Normalize: lowercase + trim + remove extra spaces
        var normalize = (string s) => Regex.Replace(s.Trim().ToLowerInvariant(), @"\s+", " ");
        var studentNorm = normalize(a.TextInput ?? "");
        var correctAnswers = q.Answers.Where(x => x.IsCorrect).Select(x => normalize(x.Content));
        var isCorrect = correctAnswers.Any(c => c == studentNorm);
        return new GradeResult { IsCorrect = isCorrect, Points = isCorrect ? q.Points : 0 };
    }

    private GradeResult GradeMultiple(Question q, ExamAttemptAnswer a)
    {
        // MultipleChoice: đúng hết mới được điểm (có thể điều chỉnh thành partial credit)
        var correctIds = q.Answers.Where(x => x.IsCorrect).Select(x => x.Id).OrderBy(x => x).ToList();
        var studentIds = JsonSerializer.Deserialize<List<int>>(a.AnswerIds ?? "[]").OrderBy(x => x).ToList();
        var isCorrect = correctIds.SequenceEqual(studentIds);
        return new GradeResult { IsCorrect = isCorrect, Points = isCorrect ? q.Points : 0 };
    }
}
```

---

## 9. Caching

```csharp
// Dùng IMemoryCache — inject vào constructor

// Cache danh sách môn học (ít thay đổi)
var subjects = await _cache.GetOrCreateAsync("subjects_active", async entry => {
    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
    return await _db.Subjects.AsNoTracking().Where(s => s.IsActive).ToListAsync();
});

// Invalidate khi Admin thêm/sửa môn:
_cache.Remove("subjects_active");

// KHÔNG cache đề thi đang diễn ra (dữ liệu nhạy cảm, per-user)
```

---

## 10. Controllers & API Endpoints

### 10.1 MVC Controllers (Web)

| Controller | Actions |
|-----------|---------|
| `AccountController` | GET/POST Login, POST Logout |
| `AdminController` | Index (dashboard), Users CRUD, Subjects CRUD |
| `TeacherController` | Questions CRUD, Exams CRUD, Results/Stats |
| `StudentController` | Dashboard (exam list), StartExam, TakeExam, Result |
| `HomeController` | Index (redirect theo role) |

### 10.2 API Controllers

| Endpoint | Method | Auth | Mô tả |
|---------|--------|------|-------|
| `/api/auth/login` | POST | — | Trả JWT token |
| `/api/auth/logout` | POST | Any | Xóa session |
| `/api/exam/start/{examId}` | POST | Student | Tạo ExamAttempt, snapshot câu hỏi |
| `/api/exam/question/{attemptId}/{index}` | GET | Student | Lấy câu hỏi theo thứ tự |
| `/api/exam/answer` | POST | Student | Lưu đáp án (upsert) |
| `/api/exam/submit/{attemptId}` | POST | Student | Nộp bài, chấm điểm |
| `/api/exam/time-remaining/{attemptId}` | GET | Student | Server time còn lại |
| `/api/exam/flag` | POST | Student | Ghi nhận vi phạm anti-cheat |
| `/api/teacher/stats/{examId}` | GET | Teacher | Thống kê kết quả |
| `/api/admin/users` | GET/POST/PUT/DELETE | Admin | CRUD users |
| `/api/admin/subjects` | GET/POST/PUT/DELETE | Admin | CRUD subjects |

---

## 11. Views & UI

### Hệ màu sắc

```css
:root {
  --primary: #185FA5;
  --primary-light: #E6F1FB;
  --primary-mid: #378ADD;
  --success: #3B6D11;
  --success-light: #EAF3DE;
  --danger: #A32D2D;
  --danger-light: #FCEBEB;
  --warning: #854F0B;
  --warning-light: #FAEEDA;
  --text-primary: #1a1a1a;
  --text-secondary: #6b7280;
  --border: rgba(0,0,0,0.12);
  --surface: #f8f8f7;
}
```

### Layout chung

```html
<!-- _Layout.cshtml -->
<nav class="navbar">
  <div class="nav-logo">ExamHub</div>
  <div class="nav-links"><!-- theo role --></div>
  <div class="nav-user"><!-- avatar + tên --></div>
</nav>
<main class="container">@RenderBody()</main>
```

### Danh sách màn hình

| # | Màn hình | Route | Role |
|---|----------|-------|------|
| 1 | Đăng nhập | `/account/login` | Public |
| 2 | Quản lý người dùng | `/admin/users` | Admin |
| 3 | Quản lý môn thi | `/admin/subjects` | Admin |
| 4 | Ngân hàng câu hỏi | `/teacher/questions` | Teacher |
| 5 | Tạo / sửa kỳ thi | `/teacher/exams/create` | Teacher |
| 6 | Thống kê kết quả | `/teacher/results/{examId}` | Teacher |
| 7 | Dashboard thí sinh | `/student/dashboard` | Student |
| 8 | Làm bài thi | `/student/exam/{attemptId}` | Student |
| 9 | Kết quả bài thi | `/student/result/{attemptId}` | Student |

### Màn hình làm bài (chi tiết)

```
Layout: 70% (câu hỏi) | 30% (sidebar điều hướng)

Top bar:
  [← Thoát]  [Tên kỳ thi — Câu X/40]  [⏱ HH:MM:SS — đỏ khi <5 phút]

Cột trái:
  - Số câu, badge loại câu hỏi
  - Nội dung câu hỏi (font 16px, line-height 1.7)
  - SingleChoice: radio buttons
  - MultipleChoice: checkboxes
  - TextAnswer: <input type="text">
  - Nút [← Câu trước] [Câu tiếp →] [⚑ Đánh dấu]

Cột phải (sidebar):
  - Progress bar: X/40 câu đã làm
  - Grid 5×8 ô số câu:
      Xanh = đang làm
      Xám đậm = đã trả lời
      Trắng = chưa làm
      Vàng = đã đánh dấu
  - [Nộp bài thi] — sticky bottom, full width

Banner anti-cheat (hiện khi tab switch):
  Cảnh báo N/3: Chuyển tab sẽ bị ghi nhận vi phạm!
```

---

## 12. Cấu hình môi trường

### appsettings.json (không commit secret)

```json
{
  "ConnectionStrings": {
    "Default": ""
  },
  "Jwt": {
    "Secret": "",
    "Issuer": "ExamHub",
    "Audience": "ExamHubUsers",
    "ExpiryMinutes": 60
  },
  "Logging": {
    "LogLevel": { "Default": "Information" }
  }
}
```

### Environment variables (production)

```bash
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__Default=Server=...;Database=ExamHub;...
JWT_SECRET=your-super-secret-key-min-32-chars
```

### .env.example (commit file này, không commit .env)

```
ConnectionStrings__Default=Server=localhost;Database=ExamHub;User Id=sa;Password=YourPwd;
JWT_SECRET=replace-with-secure-random-string-32chars
```

---

## 13. Dockerfile

```dockerfile
# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY *.sln .
COPY ExamHub.Core/*.csproj ExamHub.Core/
COPY ExamHub.Infrastructure/*.csproj ExamHub.Infrastructure/
COPY ExamHub.Web/*.csproj ExamHub.Web/
RUN dotnet restore
COPY . .
RUN dotnet publish ExamHub.Web -c Release -o /app/publish

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "ExamHub.Web.dll"]
```

### docker-compose.yml

```yaml
version: '3.9'
services:
  web:
    build: .
    ports:
      - "8080:8080"
    environment:
      - ConnectionStrings__Default=Server=db;Database=ExamHub;User Id=sa;Password=${DB_PASSWORD};TrustServerCertificate=True
      - JWT_SECRET=${JWT_SECRET}
    depends_on:
      - db

  db:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      - SA_PASSWORD=${DB_PASSWORD}
      - ACCEPT_EULA=Y
    ports:
      - "1433:1433"
    volumes:
      - sqldata:/var/opt/mssql

volumes:
  sqldata:
```

---

## 14. NuGet packages cần cài

```xml
<!-- ExamHub.Infrastructure.csproj -->
<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="8.*" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="8.*" />
<PackageReference Include="BCrypt.Net-Next" Version="4.*" />

<!-- ExamHub.Web.csproj -->
<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="8.*" />
<PackageReference Include="FluentValidation.AspNetCore" Version="11.*" />
<PackageReference Include="Serilog.AspNetCore" Version="8.*" />
```

---

## 15. Thứ tự triển khai cho AI agent

Thực hiện tuần tự các bước sau, kiểm tra build thành công trước khi sang bước tiếp:

1. **Tạo solution và projects** — `dotnet new sln`, thêm 4 projects.
2. **ExamHub.Core** — định nghĩa tất cả entities, enums, interface repositories.
3. **ExamHub.Infrastructure** — implement DbContext, migrations, SeedData, repositories.
4. **Chạy migration** — `dotnet ef migrations add InitialCreate`, `dotnet ef database update`.
5. **Bảo mật** — implement BCrypt hash, JWT service, SingleSessionMiddleware, CSP middleware.
6. **API Controllers** — auth, exam, teacher, admin endpoints (theo mục 10.2).
7. **MVC Controllers + Views** — theo thứ tự: Login → Admin → Teacher → Student.
8. **JavaScript** — `exam.js` (timer sync, anti-cheat, answer auto-save).
9. **CSS** — `site.css` dùng hệ màu mục 11, responsive breakpoints.
10. **Tests** — GradingService unit tests, auth tests.
11. **Dockerfile + docker-compose** — build, chạy `docker compose up`.
12. **Seed data** — chạy lần đầu khi `ASPNETCORE_ENVIRONMENT=Development`.

---

## 16. Checklist nghiệm thu

- [ ] Đăng nhập phân quyền đúng 3 role, sai mật khẩu 5 lần bị block 1 phút
- [ ] Một tài khoản chỉ có 1 session, đăng nhập máy khác bị kick
- [ ] Admin CRUD users, khóa/mở khóa tài khoản
- [ ] Admin CRUD subjects
- [ ] Teacher thêm câu hỏi 3 loại, import Excel
- [ ] Teacher tạo kỳ thi theo ByCount và ByPoints
- [ ] Teacher xem thống kê: danh sách thí sinh, điểm, tỷ lệ đúng từng câu
- [ ] Teacher xuất kết quả Excel
- [ ] Thí sinh xem dashboard kỳ thi đang mở
- [ ] Thí sinh bắt đầu thi — snapshot đề ngẫu nhiên đúng số lượng
- [ ] Timer đếm ngược đồng bộ server, tự nộp khi hết giờ
- [ ] Anti-cheat: cảnh báo khi chuyển tab, nộp tự động sau 3 lần
- [ ] Chấm điểm đúng: SingleChoice, MultipleChoice, TextAnswer (case-insensitive)
- [ ] Trang kết quả hiển thị đúng/sai từng câu, điểm, xếp hạng, thứ hạng lớp
- [ ] Responsive trên mobile (breakpoint 768px)
- [ ] Dockerfile build thành công, docker compose up chạy được

---

*Tài liệu sinh ngày 02/06/2026. Phiên bản 1.0.*
