# Somee Setup for QuizArena

## 1. Tạo database

Bạn đã tạo database `QuizArena` trên Somee.

Thông tin cần giữ lại:

- SQL Server address: `QuizArena.mssql.somee.com`
- Database name: `QuizArena`
- Login name: lấy trong Somee dashboard
- Password: lấy/copy trong Somee dashboard
- Connection string: copy trong Somee dashboard

Không commit connection string có password vào repo.

## 2. Tạo bảng


```sql
SELECT COUNT(*) FROM Users;
SELECT COUNT(*) FROM Subjects;
SELECT COUNT(*) FROM Questions;
SELECT COUNT(*) FROM Exams;
```


## 3. Seed dữ liệu bằng QuizArena.Seeder

Chạy từ máy local:

```bash
export ConnectionStrings__Default="<Somee connection string>"
dotnet run --project QuizArena.Seeder/QuizArena.Seeder.csproj
```

Hoặc tạo file `.env` ở thư mục gốc project. File này đã được `.gitignore`, không commit lên GitHub:

```text
ConnectionStrings__Default=<Somee connection string>
JWT_SECRET=dev-quizarena-secret-key-32chars-minimum
MIGRATE_ON_START=false
SEED_ON_START=false
```

Sau đó chạy:

```bash
dotnet run --project QuizArena.Seeder/QuizArena.Seeder.csproj
```

Seeder sẽ:

- Chạy migration nếu còn thiếu.
- Thêm 4 user seed.
- Thêm 2 subjects.
- Thêm 20 questions.
- Thêm 1 exam.

Kết quả đúng:

```text
Done. Users=4, Subjects=2, Questions=20, Exams=1
```

## 4. Tài khoản đăng nhập

- Admin: `admin_root` / `Admin@123456`
- Teacher: `gv_tranvan` / `Teacher@123`
- Student: `ts_nguyen01` / `Student@123`
- Student: `ts_lehoang` / `Student@123`

## 5. Env khi deploy web lên Render/Koyeb

```text
ConnectionStrings__Default=<Somee connection string>
JWT_SECRET=<secret dài ít nhất 32 ký tự>
ASPNETCORE_ENVIRONMENT=Production
MIGRATE_ON_START=true
SEED_ON_START=true
```

Sau khi web chạy lần đầu và DB đã seed, có thể đổi:

```text
MIGRATE_ON_START=false
SEED_ON_START=false
```

để tránh app kiểm tra DB mỗi lần khởi động.

## 6. Chạy local dùng trực tiếp DB Somee

1. Dán connection string Somee thật vào `.env`:

```text
ConnectionStrings__Default=workstation id=QuizArena.mssql.somee.com;packet size=4096;user id=<login>;pwd=<password>;data source=QuizArena.mssql.somee.com;persist security info=False;initial catalog=QuizArena;TrustServerCertificate=True
JWT_SECRET=dev-quizarena-secret-key-32chars-minimum
MIGRATE_ON_START=false
SEED_ON_START=false
```

2. Nếu Somee chưa có dữ liệu seed, chạy:

```bash
dotnet run --project QuizArena.Seeder/QuizArena.Seeder.csproj
```

3. Chạy web local:

```bash
ASPNETCORE_ENVIRONMENT=Production dotnet run --no-launch-profile --project QuizArena.Web --urls http://localhost:5227
```

Mở:

```text
http://localhost:5227/dang-nhap
```
