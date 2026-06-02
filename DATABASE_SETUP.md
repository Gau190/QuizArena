# ExamHub Database Setup

## Option 1: SQL Server bằng Docker

Tạo file `.env` từ `.env.example`, rồi chạy:

```bash
docker compose up -d db
dotnet tool run dotnet-ef database update --project ExamHub.Infrastructure/ExamHub.Infrastructure.csproj --startup-project ExamHub.Web/ExamHub.Web.csproj
dotnet run --project ExamHub.Web/ExamHub.Web.csproj
```

Khi chạy `ExamHub.Web` ở môi trường `Development`, app cũng tự chạy migration và seed data.

## Option 2: Chạy full stack bằng Docker Compose

```bash
docker compose up --build
```

Web: `http://localhost:8080`

API: `http://localhost:8081`

SQL Server: `localhost,1433`

## Tài khoản seed

- Admin: `admin_root` / `Admin@123456`
- Teacher: `gv_tranvan` / `Teacher@123`
- Student: `ts_nguyen01` / `Student@123`
- Student: `ts_lehoang` / `Student@123`

## Ghi chú

- Migration hiện có: `InitialCreate`.
- Development connection string nằm trong `ExamHub.Web/appsettings.Development.json` và `ExamHub.Api/appsettings.Development.json`.
- Production vẫn nên dùng biến môi trường `ConnectionStrings__Default` và `JWT_SECRET`.
