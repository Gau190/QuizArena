# Deploy QuizArena Free / Low-Cost

## Khuyến nghị nhanh

Vì QuizArena dùng ASP.NET Core 8 + SQL Server, phương án ít phải sửa code nhất là Azure:

- Database: Azure SQL Database Free Offer.
- Web: Azure App Service Free F1 cho demo/đồ án, hoặc Azure Container Apps Consumption nếu muốn scale linh hoạt hơn.

Lưu ý quan trọng: không có gói "free forever" nào bảo đảm production ổn định cho 100 người online cùng lúc. Free tier phù hợp demo, đồ án, test thực tế nhỏ. Nếu thi thật có 100 người vào cùng lúc, nên có ngân sách tối thiểu hoặc dùng Azure for Students credit.

## Phương án A: Dễ nhất, gần như miễn phí

### 1. Azure SQL Database Free

Tạo Azure SQL Database bằng portal:

1. Azure Portal -> Azure SQL.
2. Create database.
3. Chọn Start free / Apply free offer.
4. Database name: `QuizArena`.
5. Server auth: SQL authentication.
6. Ghi lại:
   - Server name, ví dụ: `quizarena-sql.database.windows.net`
   - Login, ví dụ: `quizarena_admin`
   - Password

Connection string mẫu:

```text
Server=tcp:quizarena-sql.database.windows.net,1433;Initial Catalog=QuizArena;Persist Security Info=False;User ID=quizarena_admin;Password=YOUR_PASSWORD;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
```

Sau khi tạo DB, chạy migration:

```bash
dotnet tool restore
dotnet tool run dotnet-ef database update \
  --project QuizArena.Infrastructure/QuizArena.Infrastructure.csproj \
  --startup-project QuizArena.Web/QuizArena.Web.csproj
```

Nếu không muốn dùng EF CLI, chạy script SQL:

```text
database/QuizArena_InitialCreate.sql
```

Seed data sẽ tự chạy khi `QuizArena.Web` chạy ở `Development`. Với production, có thể tạm set `ASPNETCORE_ENVIRONMENT=Development` lần đầu để seed, sau đó đổi lại `Production`.

### 2. Azure App Service Free F1

Dùng cho demo/đồ án. Không nên dùng cho kỳ thi thật đông người.

Tạo App Service:

1. Runtime stack: `.NET 8`.
2. OS: Linux hoặc Windows đều được.
3. Pricing plan: Free F1.

App Settings cần đặt:

```text
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__Default=Server=tcp:quizarena-sql.database.windows.net,1433;Initial Catalog=QuizArena;Persist Security Info=False;User ID=quizarena_admin;Password=YOUR_PASSWORD;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
JWT_SECRET=replace-with-secure-random-string-32chars-minimum
```

Deploy:

```bash
dotnet publish QuizArena.Web/QuizArena.Web.csproj -c Release -o publish
```

Sau đó upload bằng Azure Portal, VS Code Azure extension, hoặc GitHub Actions.

## Phương án B: Tốt hơn cho dưới 100 online, vẫn có free grant

Web chạy bằng Azure Container Apps Consumption, DB vẫn dùng Azure SQL Free.

Ưu điểm:

- Có thể scale theo request.
- Phù hợp hơn App Service Free nếu traffic có lúc tăng.

Nhược điểm:

- Cấu hình phức tạp hơn.
- Nếu vượt free grant có thể phát sinh phí.
- Có cold start nếu scale về 0.

Các bước:

1. Build image từ Dockerfile:

```bash
docker build -t quizarena-web .
```

2. Push image lên Azure Container Registry hoặc registry khác.
3. Tạo Azure Container App dùng image đó.
4. Đặt env:

```text
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__Default=<Azure SQL connection string>
JWT_SECRET=<secret min 32 chars>
```

5. Set min replicas:

```text
min replicas = 0 nếu muốn tiết kiệm tối đa
min replicas = 1 nếu muốn giảm cold start nhưng có thể vượt free grant
```

## Phương án C: Nếu có Azure for Students

Nếu bạn là sinh viên, Azure for Students có credit miễn phí. Khi đó nên chạy:

- Web: App Service Basic B1 hoặc Container Apps min replica 1.
- DB: Azure SQL Database Free trước, nếu thiếu thì nâng lên serverless trả phí bằng credit.

Đây là phương án hợp lý nhất nếu cần khoảng 100 user online cùng lúc mà vẫn muốn không bỏ tiền ban đầu.

## Cấu hình cần kiểm tra sau deploy

1. Login:
   - `admin_root` / `Admin@123456`
   - `gv_tranvan` / `Teacher@123`
   - `ts_nguyen01` / `Student@123`

2. Redirect:
   - Admin -> `/quan-tri/tong-quan`
   - Teacher -> `/giang-vien/ngan-hang-cau-hoi`
   - Student -> `/thi-sinh/tong-quan`

3. Database:

```sql
SELECT COUNT(*) FROM Users;
SELECT COUNT(*) FROM Subjects;
SELECT COUNT(*) FROM Questions;
SELECT COUNT(*) FROM Exams;
```

4. Nếu login lỗi 500, kiểm tra:
   - `ConnectionStrings__Default`
   - firewall của Azure SQL đã allow App Service/Container App
   - `JWT_SECRET` dài ít nhất 32 ký tự

## Kết luận

- Demo/đồ án: Azure App Service F1 + Azure SQL Free.
- Thi thử có dưới 100 online trong thời gian ngắn: Azure Container Apps Consumption + Azure SQL Free.
- Thi thật, cần ổn định: dùng Azure for Students credit hoặc trả phí nhỏ cho App Service Basic B1/Container Apps min replica 1.

## Có dịch vụ khác free hơn Azure không?

Có, nhưng cần phân biệt theo database.

### Nếu giữ nguyên SQL Server

Azure vẫn là phương án hợp lý nhất, vì app hiện tại dùng EF Core SQL Server:

```csharp
options.UseSqlServer(...)
```

Các nền tảng free khác thường không có managed SQL Server miễn phí lâu dài. Nếu vẫn muốn SQL Server mà không dùng Azure, thường phải tự chạy SQL Server trong container/VM, khó vận hành và dễ vượt free tier.

### Nếu chấp nhận đổi sang PostgreSQL

Có nhiều lựa chọn free hơn cho database:

| Dịch vụ | Free lâu dài | Ghi chú |
|---|---:|---|
| Aiven PostgreSQL Free | Có | 1 CPU, 1 GB RAM, 1 GB storage, phù hợp đồ án nhỏ |
| Supabase Free | Có | PostgreSQL + dashboard dễ dùng, có giới hạn tài nguyên |
| Neon Free | Có | PostgreSQL serverless, scale-to-zero, hợp app ít truy cập liên tục |
| Render Postgres Free | Có giới hạn | Dễ deploy cùng web, không dùng production |

Nếu đổi sang PostgreSQL, cần sửa project:

1. Thay package EF SQL Server bằng `Npgsql.EntityFrameworkCore.PostgreSQL`.
2. Đổi `UseSqlServer(...)` thành `UseNpgsql(...)`.
3. Xóa migration SQL Server hiện tại.
4. Tạo lại migration PostgreSQL.
5. Cập nhật connection string.

### Free web host ngoài Azure

| Dịch vụ | Free web app | Phù hợp |
|---|---:|---|
| Render Free Web Service | Có | Demo, hobby, có thể sleep/suspend |
| Koyeb Free | Có | Web service/container nhỏ |
| Fly.io | Không còn free lâu dài cho user mới | Chỉ trial ngắn |
| Railway | Chủ yếu trial/credit | Không phù hợp mục tiêu 6 tháng free chắc chắn |

### Phương án free hơn Azure nhưng phải đổi DB

Khuyến nghị nếu mục tiêu là chạy 6 tháng free:

```text
Web: Render Free hoặc Koyeb Free
DB: Aiven PostgreSQL Free hoặc Supabase Free
```

Đổi lại, cần chuyển code từ SQL Server sang PostgreSQL.

### Phương án giữ code ít sửa nhất

```text
Web: Azure App Service Free F1 hoặc Azure Container Apps Consumption
DB: Azure SQL Database Free
```

Đây vẫn là phương án ít rủi ro kỹ thuật nhất cho QuizArena hiện tại.

## Phương án Somee SQL Server + Render/Koyeb Web

Có thể dùng Somee, vì Somee free hosting hiện hỗ trợ ASP.NET Core và có 1 MS SQL database miễn phí. Theo trang Somee free hosting, gói free có các giới hạn đáng chú ý:

- 1 website.
- 150 MB web storage.
- 5 GB transfer/tháng.
- 1 MS SQL database.
- SQL database size 30 MB.
- SQL log size 30 MB.
- Có quảng cáo tự chèn.
- Website/DB có thể bị xóa nếu không hoạt động trong 30 ngày.

Nguồn: https://somee.com/FreeAspNetHosting.aspx

### Có nên dùng Somee làm DB không?

Được, nếu đây là đồ án/demo/thi thử nhỏ. Với QuizArena hiện tại, 30 MB DB có thể đủ cho:

- 4 user seed.
- 2 môn.
- 20 câu hỏi seed.
- Một số kỳ thi và bài làm thử.

Nhưng nếu bạn chạy thật với khoảng 1000 user, DB 30 MB có thể đầy nhanh do các bảng:

- `ExamAttempts`
- `ExamAttemptAnswers`
- `ExamQuestionSnapshots`
- `ActiveSessions`

Đặc biệt mỗi bài thi sẽ tạo snapshot câu hỏi và lưu đáp án từng câu.

### Kiến trúc có thể dùng

```text
Web: Render Free hoặc Koyeb Free
DB: Somee MS SQL Free
```

Ưu điểm:

- Giữ nguyên SQL Server, không cần đổi sang PostgreSQL.
- Ít sửa code.
- Có thể dùng connection string SQL Server hiện tại.

Nhược điểm:

- DB free chỉ 30 MB.
- Có thể latency cao vì web host và DB khác nhà cung cấp.
- Một số free SQL host có thể giới hạn remote connection hoặc connection count.
- Render free/Koyeb free không phù hợp production tải cao.

### Connection string mẫu cho Somee

Somee thường cung cấp connection string trong dashboard. Dạng thường gặp:

```text
workstation id=<DB_NAME>.mssql.somee.com;packet size=4096;user id=<USER>;pwd=<PASSWORD>;data source=<DB_NAME>.mssql.somee.com;persist security info=False;initial catalog=<DB_NAME>;TrustServerCertificate=True
```

Đưa vào Render/Koyeb dưới biến môi trường:

```text
ConnectionStrings__Default=<connection string Somee>
JWT_SECRET=<secret dài ít nhất 32 ký tự>
ASPNETCORE_ENVIRONMENT=Production
```

### Deploy Web lên Render

Render hỗ trợ web service từ Git repo hoặc Docker image. Với QuizArena, nên dùng Dockerfile trong repo.

Render cần app bind public HTTP port. Dockerfile hiện dùng:

```text
ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
```

Khi tạo Render Web Service:

- Runtime: Docker.
- Dockerfile: `Dockerfile`.
- Port: `8080` nếu Render yêu cầu khai báo port.
- Env vars:

```text
ConnectionStrings__Default=<Somee SQL connection string>
JWT_SECRET=<secret dài ít nhất 32 ký tự>
ASPNETCORE_ENVIRONMENT=Production
```

Nguồn Render Docker/web service: https://render.com/docs/web-services/ và https://render.com/docs/docker

### Deploy Web lên Koyeb

Koyeb hỗ trợ Dockerfile và prebuilt Docker image.

Khi tạo service:

- Deploy from GitHub hoặc Docker image.
- Builder: Dockerfile.
- Port: `8080`.
- Env vars:

```text
ConnectionStrings__Default=<Somee SQL connection string>
JWT_SECRET=<secret dài ít nhất 32 ký tự>
ASPNETCORE_ENVIRONMENT=Production
```

Nguồn Koyeb Docker deployment: https://www.koyeb.com/docs/build-and-deploy

### Migration DB Somee

Cách 1: chạy EF migration từ máy bạn:

```bash
dotnet tool restore
dotnet tool run dotnet-ef database update \
  --project QuizArena.Infrastructure/QuizArena.Infrastructure.csproj \
  --startup-project QuizArena.Web/QuizArena.Web.csproj
```

Trước khi chạy, set connection string Somee bằng environment variable:

```bash
export ConnectionStrings__Default="<Somee SQL connection string>"
export JWT_SECRET="replace-with-secure-random-string-32chars"
```

Cách 2: chạy SQL script trực tiếp trong Somee SQL manager:

```text
database/QuizArena_InitialCreate.sql
```

Sau đó cần seed data. Cách nhanh nhất là tạm chạy Web ở `Development` lần đầu để `SeedData.EnsureSeededAsync()` chạy. Nếu không muốn bật Development trên production, cần tạo một command/endpoint seed riêng hoặc insert seed data bằng SQL.

### Kết luận cho Somee

Nếu bạn muốn giữ SQL Server và không muốn dùng Azure, Somee là phương án đáng thử.

Nhưng với mục tiêu 6 tháng và khoảng dưới 100 user online:

- Somee DB free phù hợp demo/đồ án.
- Không nên kỳ vọng ổn định cho thi thật.
- Nếu DB đầy hoặc chậm, chuyển sang Azure SQL Free sẽ ít sửa code nhất.
