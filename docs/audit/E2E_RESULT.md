# Kết quả kiểm thử E2E

Máy chủ: `http://localhost:5299` · Thời điểm: 2026-09-25 07:41

**114/114 PASS**

| Nhóm | Kiểm tra | Kết quả |
|---|---|---|
| Công khai | Trang chủ 200  | PASS |
| Công khai | Trang đăng nhập 200  | PASS |
| Công khai | Header CSP  | PASS |
| Công khai | Header X-Frame-Options=DENY  | PASS |
| Công khai | Header X-Content-Type-Options  | PASS |
| Công khai | Health /trang-thai  | PASS |
| Đường dẫn | Link cũ /admin không còn (404) | PASS |
| Đường dẫn | Link cũ /teacher không còn (404) | PASS |
| Đường dẫn | Link cũ /student không còn (404) | PASS |
| Đường dẫn | Link cũ /account/login không còn (404) | PASS |
| Đường dẫn | Link cũ /api/exam/start/1 không còn (404) | PASS |
| Xác thực | Ẩn danh /quan-tri bị chuyển/chặn (302) | PASS |
| Xác thực | Ẩn danh /giang-vien bị chuyển/chặn (302) | PASS |
| Xác thực | Ẩn danh /thi-sinh bị chuyển/chặn (302) | PASS |
| Xác thực | Ẩn danh /lich-thi bị chuyển/chặn (302) | PASS |
| Xác thực | Ẩn danh /hoc-ba bị chuyển/chặn (302) | PASS |
| Xác thực | Ẩn danh /bao-cao bị chuyển/chặn (302) | PASS |
| Xác thực | Ẩn danh /thong-bao bị chuyển/chặn (302) | PASS |
| Xác thực | Ẩn danh /api/v1/admin/users bị chuyển/chặn (302) | PASS |
| Đăng nhập | Sai mật khẩu → báo lỗi, không đăng nhập  | PASS |
| Đăng nhập | SQL injection ở username bị từ chối  | PASS |
| CSRF | POST đăng nhập không có token → 400 (400) | PASS |
| Đăng nhập | Admin → /quan-tri  | PASS |
| Đăng nhập | Giảng viên → /giang-vien  | PASS |
| Đăng nhập | Thí sinh → /thi-sinh  | PASS |
| Đăng nhập | Thí sinh 2 → /thi-sinh  | PASS |
| Phân quyền | Thí sinh bị chặn /quan-tri (302) | PASS |
| Phân quyền | Thí sinh bị chặn /quan-tri/nguoi-dung (302) | PASS |
| Phân quyền | Thí sinh bị chặn /quan-tri/cai-dat (302) | PASS |
| Phân quyền | Thí sinh bị chặn /giang-vien (302) | PASS |
| Phân quyền | Thí sinh bị chặn /giang-vien/ky-thi/tao (302) | PASS |
| Phân quyền | Thí sinh bị chặn /bao-cao (302) | PASS |
| Phân quyền | Thí sinh bị chặn /api/v1/admin/users (302) | PASS |
| Phân quyền | Giảng viên bị chặn /quan-tri (302) | PASS |
| Phân quyền | Giảng viên bị chặn /quan-tri/nguoi-dung (302) | PASS |
| Phân quyền | Giảng viên bị chặn /api/v1/admin/users (302) | PASS |
| Phân quyền | Admin không vào khu thí sinh /thi-sinh (302) | PASS |
| Phân quyền | Admin không vào khu thí sinh /api/v1/exam/time-remaining/f651666c-c359-44e0-aded-a26e8bb1bf57 (302) | PASS |
| Trang chính | GET /quan-tri (200) | PASS |
| Trang chính | GET /quan-tri/lop-hoc (200) | PASS |
| Trang chính | GET /quan-tri/nam-hoc (200) | PASS |
| Trang chính | GET /quan-tri/phong-thi (200) | PASS |
| Trang chính | GET /quan-tri/nguoi-dung (200) | PASS |
| Trang chính | GET /quan-tri/giao-vien (200) | PASS |
| Trang chính | GET /quan-tri/mon-thi (200) | PASS |
| Trang chính | GET /quan-tri/cai-dat (200) | PASS |
| Trang chính | GET /bao-cao (200) | PASS |
| Trang chính | GET /thong-bao (200) | PASS |
| Trang chính | GET /lich-thi (200) | PASS |
| Trang chính | GET /quan-tri/lop-hoc/xuat (200) | PASS |
| Trang chính | GET /bao-cao/xuat (200) | PASS |
| Trang chính | GET /giang-vien (200) | PASS |
| Trang chính | GET /giang-vien (200) | PASS |
| Trang chính | GET /giang-vien/ngan-hang-cau-hoi/cau-hoi-yeu (200) | PASS |
| Trang chính | GET /giang-vien/cau-truc-de (200) | PASS |
| Trang chính | GET /giang-vien/ky-thi/tao (200) | PASS |
| Trang chính | GET /giang-vien/ket-qua (200) | PASS |
| Trang chính | GET /giang-vien/phan-hoi-cau-hoi (200) | PASS |
| Trang chính | GET /lich-thi (200) | PASS |
| Trang chính | GET /ho-so (200) | PASS |
| Trang chính | GET /bao-cao (200) | PASS |
| Trang chính | GET /thong-bao (200) | PASS |
| Trang chính | GET /thi-sinh (200) | PASS |
| Trang chính | GET /lich-thi (200) | PASS |
| Trang chính | GET /ho-so (200) | PASS |
| Trang chính | GET /hoc-ba (200) | PASS |
| Trang chính | GET /hoc-ba/xuat (404) | PASS |
| Trang chính | GET /thong-bao (200) | PASS |
| Thuật ngữ | Các trang chính dùng thống nhất Giáo viên/Thí sinh, không viết tắt  | PASS |
| Thuật ngữ | Các trang chính dùng thống nhất Giáo viên/Thí sinh, không viết tắt  | PASS |
| Thuật ngữ | Các trang chính dùng thống nhất Giáo viên/Thí sinh, không viết tắt  | PASS |
| Bảo mật | /trang-thai/csdl không lộ số tài khoản  | PASS |
| Admin | Mật khẩu < 6 ký tự bị từ chối  | PASS |
| Admin | Tạo người dùng thành công  | PASS |
| Admin | Tài khoản mới đăng nhập được  | PASS |
| Admin | Tạo môn thi  | PASS |
| API | GET /api/v1/admin/users 200  | PASS |
| API | Không lộ passwordHash  | PASS |
| API | Tạo user dữ liệu sai → 400 (400) | PASS |
| Giảng viên | Thêm câu hỏi vào ngân hàng  | PASS |
| Giao diện | Ngân hàng câu hỏi hiện nhãn tiếng Việt, không hiện enum thô  | PASS |
| Bảo mật | XSS được mã hoá khi hiển thị  | PASS |
| Giảng viên | Câu 1 đáp án nhưng 2 đúng bị từ chối  | PASS |
| Giảng viên | GET /giang-vien/ket-qua (200) | PASS |
| Giảng viên | GET /giang-vien/ket-qua/1 (200) | PASS |
| Giảng viên | GET /giang-vien/ket-qua/1/xuat (200) | PASS |
| Giảng viên | Import CSV 4 câu hỏi (302) | PASS |
| Xuất file | Báo cáo xuất .xlsx hợp lệ (zip) (200, 1625B) | PASS |
| Xuất file | Danh sách lớp xuất được (200) | PASS |
| Tải tệp | Tệp .txt bị từ chối  | PASS |
| Tải tệp | Tệp CSV lớn hơn 2 MB bị từ chối  | PASS |
| Tải tệp | Thiếu tệp không gây lỗi 500  | PASS |
| Tải tệp | Logo giả (đuôi .png nhưng không phải ảnh) bị từ chối  | PASS |
| Admin | Thí sinh mới tạo có hồ sơ và hiện trong danh sách lớp  | PASS |
| Thí sinh | Có kỳ thi khả dụng trên bảng điều khiển (11 kỳ thi) | PASS |
| Thí sinh | Bắt đầu thi → tạo lượt thi /thi-sinh/lam-bai/a7eff710-cb7f-4f5d-9f3d-8e23174d5bf5 | PASS |
| Đồng thời | 10 yêu cầu bắt đầu thi cùng lúc chỉ tạo đúng 1 lượt (1 lượt) | PASS |
| Sinh đề | Đề 2 thí sinh khác nhau (ngẫu nhiên câu/thứ tự) (10 câu) | PASS |
| Sinh đề | Mỗi đề không trùng câu hỏi  | PASS |
| Thí sinh | Trang làm bài 200  | PASS |
| Bảo mật | HTML bài thi không chứa cờ đáp án đúng  | PASS |
| Bảo mật | Thí sinh khác không xem được bài  | PASS |
| API | Lấy câu hỏi 200  | PASS |
| Bảo mật | API câu hỏi KHÔNG lộ đáp án đúng  | PASS |
| API | Lưu đáp án → 204 (204) | PASS |
| Bảo mật | Lưu đáp án cho câu ngoài đề → 409 (409) | PASS |
| Bảo mật | Lưu đáp án vào bài người khác bị từ chối (409) | PASS |
| API | Đồng hồ còn lại > 0 {"remaining":3599,"autoSubmitted":false} | PASS |
| Thí sinh | Chưa nộp thì không xem kết quả  | PASS |
| Thí sinh | Nộp bài → chuyển sang kết quả /thi-sinh/ket-qua/a7eff710-cb7f-4f5d-9f3d-8e23174d5bf5 | PASS |
| Thí sinh | Trang kết quả 200  | PASS |
| Bảo mật | Thí sinh khác không xem kết quả  | PASS |
| Thí sinh | Không sửa đáp án sau khi nộp (409) | PASS |
| Bảo mật | Rate-limit đăng nhập sai liên tiếp → 429 (codes cuối: [429, 429, 429]) | PASS |
