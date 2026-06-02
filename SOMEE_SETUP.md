# Somee Setup for ExamHub

## 1. Tạo database

Bạn đã tạo database `ExamHub` trên Somee.

Thông tin cần giữ lại:

- SQL Server address: `ExamHub.mssql.somee.com`
- Database name: `ExamHub`
- Login name: lấy trong Somee dashboard
- Password: lấy/copy trong Somee dashboard
- Connection string: copy trong Somee dashboard

Không commit connection string có password vào repo.

## 2. Tạo bảng

Bạn đã chạy script:

```text
database/ExamHub_InitialCreate.sql
```

Nếu kiểm tra:

```sql
SELECT COUNT(*) FROM Users;
SELECT COUNT(*) FROM Subjects;
SELECT COUNT(*) FROM Questions;
SELECT COUNT(*) FROM Exams;
```

mà đều trả `0`, nghĩa là bảng đã có nhưng chưa seed dữ liệu.

## 3. Seed dữ liệu bằng ExamHub.Seeder

Chạy từ máy local:

```bash
export ConnectionStrings__Default="<Somee connection string>"
dotnet run --project ExamHub.Seeder/ExamHub.Seeder.csproj
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
