# Deploy ExamHub Web to Render

DB Somee của bạn đã có dữ liệu seed:

```text
Users=4, Subjects=2, Questions=20, Exams=1
```

Vì vậy deploy Render chỉ cần chạy `ExamHub.Web` và trỏ đến Somee SQL Server.

## 1. Chuẩn bị GitHub repo

Render deploy tốt nhất từ GitHub.

Đẩy source này lên GitHub, nhưng không commit connection string có password.

Các file cần có trong repo:

- `Dockerfile`
- `ExamHub.sln`
- `ExamHub.Web`
- `ExamHub.Core`
- `ExamHub.Infrastructure`
- `database/ExamHub_InitialCreate.sql`

## 2. Tạo Web Service trên Render

1. Vào Render dashboard.
2. New -> Web Service.
3. Connect GitHub repository chứa ExamHub.
4. Chọn:
   - Runtime: `Docker`
   - Dockerfile path: `Dockerfile`
   - Branch: branch bạn muốn deploy, thường là `main`
   - Region: chọn gần người dùng nhất có thể
   - Plan: Free

Dockerfile hiện expose port `8080`:

```text
ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
```

Nếu Render hỏi port/internal port, nhập:

```text
8080
```

## 3. Environment Variables trên Render

Vào Web Service -> Environment -> Add Environment Variable.

Thêm các biến:

```text
ASPNETCORE_ENVIRONMENT=Production
JWT_SECRET=replace-with-a-real-random-secret-minimum-32-characters
ConnectionStrings__Default=<Somee connection string>
MIGRATE_ON_START=false
SEED_ON_START=false
```

Vì DB Somee đã migrate và seed rồi, để `MIGRATE_ON_START=false` và `SEED_ON_START=false` cho nhẹ.

Nếu sau này reset DB, có thể tạm đổi:

```text
MIGRATE_ON_START=true
SEED_ON_START=true
```

sau lần deploy đầu tiên rồi đổi lại `false`.

## 4. Connection string Somee

Lấy connection string trong Somee dashboard.

Dạng mẫu:

```text
workstation id=ExamHub.mssql.somee.com;packet size=4096;user id=<login>;pwd=<password>;data source=ExamHub.mssql.somee.com;persist security info=False;initial catalog=ExamHub;TrustServerCertificate=True
```

Không đưa connection string vào GitHub.

## 5. Deploy

Bấm:

```text
Create Web Service
```

Render sẽ build Docker image và start app.

Sau deploy, Render cung cấp URL dạng:

```text
https://<service-name>.onrender.com
```

Mở:

```text
https://<service-name>.onrender.com/account/login
```

## 6. Tài khoản test

```text
admin_root / Admin@123456
gv_tranvan / Teacher@123
ts_nguyen01 / Student@123
ts_lehoang / Student@123
```

Redirect đúng:

```text
Admin -> /admin/dashboard
Teacher -> /teacher/questions
Student -> /student/dashboard
```

## 7. Nếu deploy lỗi

### Lỗi không kết nối DB

Kiểm tra:

- `ConnectionStrings__Default` đúng chưa.
- Somee DB còn active không.
- Password có ký tự đặc biệt bị copy thiếu không.

### Lỗi JWT

`JWT_SECRET` phải dài ít nhất 32 ký tự.

Ví dụ:

```text
examhub-production-secret-please-change-2026
```

### Render free bị sleep

Render free có thể sleep khi không có traffic. Lần mở đầu tiên có thể chậm.

## 8. Sau khi deploy xong

Vì password Somee đã từng được gửi trong chat, nên sau khi web chạy được:

1. Vào Somee.
2. Bấm `CHANGE LOGIN PASSWORD`.
3. Copy connection string mới.
4. Cập nhật lại `ConnectionStrings__Default` trên Render.
5. Redeploy/restart service.
