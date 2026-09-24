# QuizArena — Website thi trắc nghiệm trực tuyến

Bài tập lớn **Lập trình Web (IT15) – Đề 3**, Nhóm 13, Trường Đại học Mở Hà Nội.
Xây dựng bằng **ASP.NET Core 8 (MVC + Web API)**, **Entity Framework Core 8**, **MS SQL Server**.

Ba vai trò: **Quản trị viên**, **Giảng viên**, **Thí sinh**. Đề thi được sinh ngẫu nhiên từ ngân hàng câu hỏi (theo số câu, tổng điểm hoặc số câu Dễ/TB/Khó), làm bài có đồng hồ đếm ngược, chấm điểm tự động.

## Thành viên

| STT | Họ và tên | Phân công |
| --- | --- | --- |
| 1 | **Đàm Chí Công** (nhóm trưởng) | Phân tích, thiết kế CSDL, Môn thi, Cấu trúc đề |
| 2 | **Vũ Quốc Đạt** | Xác thực, Quản trị người dùng |
| 3 | **Nguyễn Hoàng Đức** | Thi trực tuyến, chấm điểm, kết quả |
| 4 | **Đậu Thế Huy** | Giao diện, Dashboard, Lớp học, Học bạ |
| 5 | **Ngô Quốc Khánh** | Ngân hàng câu hỏi, Báo cáo, kiểm thử |

## Cấu trúc thư mục

```
QuizArena/
├─ QuizArena.sln
├─ src/
│  ├─ QuizArena.Core/            thực thể, enum, giao diện dịch vụ, DTO
│  ├─ QuizArena.Infrastructure/  EF Core, migration, dịch vụ nghiệp vụ, dữ liệu mẫu
│  ├─ QuizArena.Web/             MVC + REST API (/api/v1), Razor View, wwwroot
│  ├─ QuizArena.Api/             API độc lập
│  └─ QuizArena.Seeder/          công cụ nạp dữ liệu mẫu
├─ tests/QuizArena.Tests/        xUnit (GradingService)
├─ scripts/                      run-web.sh, e2e_audit.py, capture_screens.py
├─ database/                     QuizArena_InitialCreate.sql
├─ docs/                         đặc tả, mockup, hướng dẫn triển khai, kết quả kiểm thử
├─ Dockerfile, Dockerfile.api, docker-compose.yml
└─ .env.example
```

## Chạy dự án

### Docker Compose
```bash
cp .env.example .env        # đặt DB_PASSWORD, JWT_SECRET
docker compose up --build
# Web: http://localhost:8080    API: http://localhost:8081
```

### .NET CLI
```bash
cp .env.example .env        # ConnectionStrings__Default trỏ tới SQL Server
dotnet tool restore
dotnet tool run dotnet-ef database update --project src/QuizArena.Infrastructure --startup-project src/QuizArena.Web
scripts/run-web.sh          # http://localhost:5227
```

## Đường dẫn chính

| Vai trò | Đường dẫn |
| --- | --- |
| Công khai | `/`, `/dang-nhap`, `/trang-thai` |
| Quản trị | `/quan-tri`, `/quan-tri/nguoi-dung`, `/quan-tri/mon-thi`, `/quan-tri/lop-hoc`, `/quan-tri/nam-hoc`, `/quan-tri/phong-thi`, `/quan-tri/cai-dat` |
| Giảng viên | `/giang-vien/ngan-hang-cau-hoi`, `/giang-vien/cau-truc-de`, `/giang-vien/ky-thi/tao`, `/giang-vien/ket-qua` |
| Thí sinh | `/thi-sinh`, `/thi-sinh/lam-bai/{id}`, `/thi-sinh/ket-qua/{id}`, `/hoc-ba`, `/ho-so` |
| Dùng chung | `/lich-thi`, `/thong-bao`, `/bao-cao` |
| REST API | `/api/v1/auth`, `/api/v1/admin`, `/api/v1/exam`, `/api/v1/teacher` |

## Tài khoản mẫu (chỉ dùng khi seed dữ liệu ở môi trường phát triển)

| Vai trò | Tên đăng nhập | Mật khẩu |
| --- | --- | --- |
| Quản trị viên | `admin_root` | `Admin@123456` |
| Giảng viên | `gv_tranvan` | `Teacher@123` |
| Thí sinh | `ts_nguyen01`, `ts_lehoang` | `Student@123` |

Đổi toàn bộ mật khẩu mẫu và `JWT_SECRET` trước khi triển khai thật. **Không** đưa tệp `.env` (chứa chuỗi kết nối thật) vào kho mã hay tệp nộp.

## Kiểm thử

```bash
dotnet test tests/QuizArena.Tests
python3 scripts/e2e_audit.py http://localhost:5227          # E2E qua HTTP, chạy trên CSDL dev đã seed
python3 scripts/capture_screens.py http://localhost:5227 screenshots
```
Kết quả gần nhất: [docs/audit/E2E_RESULT.md](docs/audit/E2E_RESULT.md).
