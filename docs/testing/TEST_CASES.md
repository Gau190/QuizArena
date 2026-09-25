# Test case và kết quả kiểm thử

Bản triển khai: https://quizarena-8rh6.onrender.com. Phân công: **Codex** rà soát tĩnh mã nguồn và nội dung giao diện (chỉ đọc); **Claude** chạy các luồng người dùng trên site thật, kiểm thử đồng thời và quét chữ hiển thị. Dữ liệu thử có tiền tố `QA` và đã được xoá khỏi CSDL sau khi chạy (mã lần chạy cuối `qa059d`). Kết quả là của lần chạy sau khi sửa lỗi.

Tổng: 134 test case, đạt 127, đạt một phần 4, không đạt 3.

| Mã | Nhóm | Nội dung kiểm tra | Người chạy | Kết quả | Ghi chú |
|---|---|---|---|---|---|
| TC-STA-01 | Rà soát tĩnh | Bảng mọi action: [Authorize], chống CSRF cho POST | Codex | Đạt | Mọi POST MVC có token; API POST/PUT/DELETE chưa có antiforgery (mức TB, có SameSite=Strict) |
| TC-STA-02 | Rà soát tĩnh | IDOR: hồ sơ, học bạ, kết quả, kỳ thi, thông báo | Codex | Đạt một phần | Thí sinh không xem được dữ liệu người khác. Giáo viên xem được báo cáo, hồ sơ, ngân hàng câu hỏi toàn trường; thông báo đọc dùng chung theo vai trò (chưa sửa) |
| TC-STA-03 | Rà soát tĩnh | Tải tệp (CSV, XLSX, logo) và CSV injection | Codex | Đạt | Đã sửa: giới hạn 2 MB và 2000 dòng, chỉ nhận .csv/.xlsx, kiểm chữ ký ảnh logo, vô hiệu công thức khi xuất CSV; kiểm chứng bằng TC-UP-01..05 |
| TC-STA-04 | Rà soát tĩnh | Kiểm tra biên dữ liệu form | Codex | Không đạt | Một số form thiếu kiểm tra null/độ dài/ngày (TB) — chưa sửa |
| TC-STA-05 | Rà soát tĩnh | Phiên, cookie, JWT | Codex | Đạt một phần | Đã sửa khoá JWT dự phòng (báo lỗi khi thiếu ở Production, 4 unit test). Cookie xác thực chưa đặt cờ tường minh (chưa sửa) |
| TC-STA-06 | Rà soát tĩnh | Logic thi phía máy chủ | Codex | Đạt một phần | Đã sửa tranh chấp bắt đầu/nộp bài (khoá trong một tiến trình, kiểm chứng TC-RACE-01/02). Cờ AutoSubmit của kỳ thi vẫn chưa được dùng |
| TC-TXT-01 | Nội dung giao diện | Rà soát chuỗi hiển thị: ghi chú lộ, vô nghĩa, xao lãng, khó hiểu, thuật ngữ | Codex | Đạt một phần | 40 phát hiện; đã sửa nhóm nội dung nội bộ, enum thô, viết tắt, thuật ngữ Giáo viên/Thí sinh. Còn một số mục nhỏ chưa xử lý |
| TC-RACE-01 | Đồng thời | 10 yêu cầu bắt đầu thi cùng lúc của một thí sinh (chạy cục bộ, sau khi sửa) | Claude | Đạt | Trước khi sửa: 12 yêu cầu tạo 7 lượt; sau khi sửa: 1 lượt |
| TC-TXT-02 | Nội dung giao diện | Quét chữ hiển thị thật trên site (3 vai trò, 26 trang) sau khi sửa | Claude | Đạt | Từ nhiều trang còn enum/tiếng Anh xuống 4/26 trang; 4 trang còn lại chỉ hiện tên cột CSV cần nhập và tên hiển thị "Admin" (chủ ý) |
| TC-DATA-01 | Dữ liệu | Dữ liệu trên CSDL đang dùng còn nhận diện cũ | Claude | Đạt | Đã dọn bằng công cụ DbCleanup: email @examhub → @quizarena, mã thí sinh HS- → TS-, thông báo thử, mã HTML thô trong tên kỳ thi/môn, dữ liệu QA; kỳ thi AUDIT_EXAM được ẩn (giữ điểm của thí sinh) |
| TC-DATA-02 | Dữ liệu | Số liệu nhất quán giữa các trang | Claude | Không đạt | Xếp hạng lớp của cùng một thí sinh là 13/17 ở trang chính nhưng 13/21 ở hồ sơ — chưa sửa |
| TC-INF-01 | Hạ tầng | Health /trang-thai | Claude | Đạt | 237 ms |
| TC-INF-02 | Hạ tầng | Kết nối CSDL SQL Server | Claude | Đạt | {"status":"ok","db":"ok","utc":"2026-09-25T01:13:46.3385181Z |
| TC-INF-07 | Hạ tầng | /trang-thai/csdl không lộ số tài khoản | Claude | Đạt |  |
| TC-INF-03 | Hạ tầng | Chạy HTTPS | Claude | Đạt |  |
| TC-INF-04 | Hạ tầng | Trang chủ 200, có header bảo mật | Claude | Đạt |  |
| TC-INF-05 | Hạ tầng | Đường dẫn không tồn tại trả 404 (không lộ lỗi hệ thống) | Claude | Đạt |  |
| TC-INF-06 | Hạ tầng | Tài nguyên tĩnh /css/site.css | Claude | Đạt | 55831B |
| TC-INF-06 | Hạ tầng | Tài nguyên tĩnh /js/exam.js | Claude | Đạt | 9439B |
| TC-INF-06 | Hạ tầng | Tài nguyên tĩnh /fonts/inter-latin-400-normal.woff2 | Claude | Đạt | 21564B |
| TC-INF-06 | Hạ tầng | Tài nguyên tĩnh /lib/fontawesome/css/all.min.css | Claude | Đạt | 103009B |
| TC-INF-06 | Hạ tầng | Tài nguyên tĩnh /favicon.svg | Claude | Đạt | 244B |
| TC-AUTH-01 | Đăng nhập | Admin đăng nhập → /quan-tri | Claude | Đạt |  |
| TC-AUTH-02 | Đăng nhập | Giáo viên đăng nhập → /giang-vien | Claude | Đạt |  |
| TC-AUTH-03 | Đăng nhập | Thí sinh đăng nhập → /thi-sinh | Claude | Đạt |  |
| TC-AUTH-04 | Đăng nhập | Sai mật khẩu: báo lỗi, không vào | Claude | Đạt |  |
| TC-AUTH-05 | Đăng nhập | Bỏ trống: báo nhập đủ thông tin | Claude | Đạt |  |
| TC-AUTH-06 | Đăng nhập | Thiếu token CSRF bị từ chối | Claude | Đạt |  |
| TC-AUTH-07 | Đăng nhập | SQL injection bị từ chối | Claude | Đạt |  |
| TC-AUTH-08 | Đăng nhập | Chưa đăng nhập vào /quan-tri bị chuyển về đăng nhập | Claude | Đạt |  |
| TC-AUTH-08 | Đăng nhập | Chưa đăng nhập vào /giang-vien bị chuyển về đăng nhập | Claude | Đạt |  |
| TC-AUTH-08 | Đăng nhập | Chưa đăng nhập vào /thi-sinh bị chuyển về đăng nhập | Claude | Đạt |  |
| TC-AUTH-08 | Đăng nhập | Chưa đăng nhập vào /hoc-ba bị chuyển về đăng nhập | Claude | Đạt |  |
| TC-AUTH-08 | Đăng nhập | Chưa đăng nhập vào /bao-cao bị chuyển về đăng nhập | Claude | Đạt |  |
| TC-AUTH-09 | Đăng nhập | Đăng nhập lần 2 làm phiên cũ hết hiệu lực (một phiên/tài khoản) | Claude | Đạt | cũ=401 mới=200 |
| TC-AUTH-10 | Đăng nhập | Sau đăng xuất, phiên cũ không dùng lại được | Claude | Đạt | 302 |
| TC-AD-01 | Quản trị | Mở /quan-tri | Claude | Đạt | 1711 ms |
| TC-AD-01 | Quản trị | Mở /quan-tri/nguoi-dung | Claude | Đạt | 976 ms |
| TC-AD-01 | Quản trị | Mở /quan-tri/mon-thi | Claude | Đạt | 969 ms |
| TC-AD-01 | Quản trị | Mở /quan-tri/lop-hoc | Claude | Đạt | 3458 ms |
| TC-AD-01 | Quản trị | Mở /quan-tri/nam-hoc | Claude | Đạt | 1590 ms |
| TC-AD-01 | Quản trị | Mở /quan-tri/phong-thi | Claude | Đạt | 2352 ms |
| TC-AD-01 | Quản trị | Mở /quan-tri/giao-vien | Claude | Đạt | 1803 ms |
| TC-AD-01 | Quản trị | Mở /quan-tri/cai-dat | Claude | Đạt | 828 ms |
| TC-AD-01 | Quản trị | Mở /bao-cao | Claude | Đạt | 1363 ms |
| TC-AD-01 | Quản trị | Mở /thong-bao | Claude | Đạt | 1099 ms |
| TC-AD-01 | Quản trị | Mở /lich-thi | Claude | Đạt | 1596 ms |
| TC-AD-02 | Quản trị | Tạo 1 giáo viên và 2 thí sinh thử | Claude | Đạt |  |
| TC-AD-03 | Quản trị | Tạo trùng tên đăng nhập bị từ chối | Claude | Đạt |  |
| TC-AD-04 | Quản trị | Tạo môn thi | Claude | Đạt |  |
| TC-AD-05 | Quản trị | Tạo lớp học | Claude | Đạt | QA-qa059d |
| TC-AD-06 | Quản trị | Chuyển thí sinh mới vào lớp (thí sinh mới tạo hiện trong danh sách lớp) | Claude | Đạt |  |
| TC-AD-07 | Quản trị | Xuất danh sách lớp | Claude | Đạt |  |
| TC-UP-05 | Tải tệp | Xuất CSV vô hiệu công thức (ô bắt đầu bằng = có dấu nháy đơn) | Claude | Đạt |  |
| TC-AD-08 | Quản trị | Xuất báo cáo Excel hợp lệ | Claude | Đạt |  |
| TC-AD-09 | Quản trị | Đặt lại mật khẩu rồi đăng nhập bằng mật khẩu mới | Claude | Đạt |  |
| TC-AD-10 | Quản trị | Khoá tài khoản: không đăng nhập được | Claude | Đạt |  |
| TC-AD-11 | Quản trị | Mở khoá: đăng nhập lại được | Claude | Đạt |  |
| TC-AD-12 | Quản trị | API người dùng: không lộ hash | Claude | Đạt |  |
| TC-GV-01 | Giáo viên | Giáo viên mới đăng nhập được | Claude | Đạt |  |
| TC-GV-02 | Giáo viên | Mở /giang-vien/ngan-hang-cau-hoi | Claude | Đạt |  |
| TC-GV-02 | Giáo viên | Mở /giang-vien/cau-truc-de | Claude | Đạt |  |
| TC-GV-02 | Giáo viên | Mở /giang-vien/ky-thi/tao | Claude | Đạt |  |
| TC-GV-02 | Giáo viên | Mở /giang-vien/ket-qua | Claude | Đạt |  |
| TC-GV-02 | Giáo viên | Mở /giang-vien/phan-hoi-cau-hoi | Claude | Đạt |  |
| TC-GV-02 | Giáo viên | Mở /giang-vien/ngan-hang-cau-hoi/cau-hoi-yeu | Claude | Đạt |  |
| TC-GV-02 | Giáo viên | Mở /lich-thi | Claude | Đạt |  |
| TC-GV-02 | Giáo viên | Mở /ho-so | Claude | Đạt |  |
| TC-GV-02 | Giáo viên | Mở /bao-cao | Claude | Đạt |  |
| TC-GV-02 | Giáo viên | Mở /thong-bao | Claude | Đạt |  |
| TC-GV-03 | Giáo viên | Môn thi mới hiện trong form câu hỏi | Claude | Đạt |  |
| TC-GV-04 | Giáo viên | Thêm 12 câu hỏi (đủ 3 dạng, 3 độ khó) | Claude | Đạt | 24 câu |
| TC-TXT-03 | Nội dung giao diện | Ngân hàng câu hỏi hiện nhãn tiếng Việt, không hiện enum thô | Claude | Đạt |  |
| TC-GV-05 | Giáo viên | Câu một đáp án có 2 đáp án đúng bị từ chối | Claude | Đạt |  |
| TC-GV-06 | Giáo viên | Nhập 3 câu từ CSV | Claude | Đạt |  |
| TC-GV-07 | Giáo viên | Nhập tệp rỗng: báo lỗi, không lỗi 500 | Claude | Đạt |  |
| TC-UP-01 | Tải tệp | Tệp .txt bị từ chối | Claude | Đạt |  |
| TC-UP-02 | Tải tệp | Tệp CSV lớn hơn 2 MB bị từ chối | Claude | Đạt |  |
| TC-UP-03 | Tải tệp | Thiếu tệp không gây lỗi 500 | Claude | Đạt | 200 |
| TC-UP-04 | Tải tệp | Logo giả (đuôi .png, nội dung không phải ảnh) bị từ chối | Claude | Đạt |  |
| TC-GV-08 | Giáo viên | Tạo kỳ thi (theo số câu, gán lớp, cho xem đáp án) | Claude | Đạt |  |
| TC-GV-09 | Giáo viên | Kỳ thi có giờ kết thúc trước giờ bắt đầu bị từ chối | Claude | Đạt |  |
| TC-GV-10 | Giáo viên | Số câu/thời lượng âm không gây lỗi 500 | Claude | Đạt | 302 |
| TC-GV-11 | Giáo viên | Giáo viên không vào được khu Quản trị | Claude | Đạt |  |
| TC-TS-01 | Thí sinh | Thí sinh mới đăng nhập được | Claude | Đạt |  |
| TC-TS-02 | Thí sinh | Mở /thi-sinh | Claude | Đạt | HTTP 200 |
| TC-TS-02 | Thí sinh | Mở /lich-thi | Claude | Đạt | HTTP 200 |
| TC-TS-02 | Thí sinh | Mở /ho-so | Claude | Đạt | HTTP 200 |
| TC-TS-02 | Thí sinh | Mở /hoc-ba | Claude | Đạt | HTTP 200 |
| TC-TS-02 | Thí sinh | Mở /thong-bao | Claude | Đạt | HTTP 200 |
| TC-TS-03 | Thí sinh | Kỳ thi QA hiện ở trang chính của thí sinh trong lớp | Claude | Đạt | exam=34 ds=['34'] |
| TC-TS-04 | Thí sinh | Bắt đầu thi | Claude | Đạt | 1 lượt |
| TC-RACE-02 | Đồng thời | 6 yêu cầu bắt đầu thi cùng lúc trên site thật chỉ tạo đúng 1 lượt | Claude | Đạt | 1 lượt |
| TC-TS-05 | Thí sinh | Bấm bắt đầu lần nữa vẫn vào lại đúng lượt đang làm (không tạo lượt mới) | Claude | Đạt |  |
| TC-TS-06 | Thí sinh | Đề có đúng 6 câu, không trùng câu | Claude | Đạt | 6 câu |
| TC-TS-07 | Thí sinh | API câu hỏi không lộ đáp án/giải thích | Claude | Đạt |  |
| TC-TS-08 | Thí sinh | Trang làm bài 200, không chứa đáp án đúng | Claude | Đạt |  |
| TC-TS-09 | Thí sinh | Lưu đáp án từng câu (204) | Claude | Đạt |  |
| TC-TS-10 | Thí sinh | Quay lại câu 1 thấy đáp án đã chọn | Claude | Đạt |  |
| TC-TS-11 | Thí sinh | Đồng hồ còn ≈ 20 phút | Claude | Đạt | 1166s |
| TC-TS-12 | Thí sinh | Báo lỗi câu hỏi | Claude | Đạt | 204 |
| TC-TS-13 | Thí sinh | Báo lỗi trùng câu bị từ chối | Claude | Đạt |  |
| TC-TS-14 | Thí sinh | Lưu đáp án cho câu ngoài đề bị từ chối | Claude | Đạt |  |
| TC-TS-15 | Thí sinh | Thí sinh khác không mở được bài đang làm | Claude | Đạt |  |
| TC-TS-16 | Thí sinh | Chưa nộp thì chưa xem được kết quả | Claude | Đạt |  |
| TC-TS-17 | Thí sinh | Nộp bài → trang kết quả | Claude | Đạt |  |
| TC-TS-18 | Thí sinh | Trang kết quả 200 | Claude | Đạt |  |
| TC-TS-19 | Thí sinh | Kết quả hiện giải thích đáp án | Claude | Đạt |  |
| TC-TS-20 | Thí sinh | Điểm được chấm (có số điểm) | Claude | Đạt |  |
| TC-TS-21 | Thí sinh | Không sửa được đáp án sau khi nộp | Claude | Đạt |  |
| TC-TS-22 | Thí sinh | Nộp lại lần hai không gây lỗi | Claude | Đạt |  |
| TC-TS-23 | Thí sinh | Thí sinh khác không xem được kết quả này | Claude | Đạt |  |
| TC-TS-24 | Thí sinh | Hết lượt thi tối đa (2): không bắt đầu được lượt 3 | Claude | Đạt |  |
| TC-TS-25 | Thí sinh | Học bạ có dữ liệu sau khi thi | Claude | Đạt |  |
| TC-TS-26 | Thí sinh | Cập nhật email hồ sơ | Claude | Đạt |  |
| TC-TS-27 | Thí sinh | Đổi mật khẩu với mật khẩu hiện tại sai bị từ chối | Claude | Đạt |  |
| TC-TS-28 | Thí sinh | Đổi mật khẩu đúng rồi đăng nhập bằng mật khẩu mới | Claude | Đạt |  |
| TC-TS-29 | Thí sinh | Đánh dấu thông báo đã đọc | Claude | Đạt |  |
| TC-TS-30 | Thí sinh | Đăng xuất | Claude | Đạt |  |
| TC-GV-12 | Giáo viên | Xem kết quả kỳ thi có bài nộp của thí sinh QA | Claude | Đạt |  |
| TC-GV-13 | Giáo viên | Xuất kết quả Excel hợp lệ | Claude | Đạt |  |
| TC-GV-14 | Giáo viên | Thấy báo lỗi câu hỏi của thí sinh | Claude | Đạt |  |
| TC-SEC-01 | Bảo mật | Giáo viên khác không xem được kết quả kỳ thi của người khác | Claude | Đạt | 302 |
| TC-SEC-02 | Bảo mật | Giáo viên khác không xoá được kỳ thi của người khác | Claude | Đạt | 302 |
| TC-SEC-03 | Bảo mật | Thí sinh xem hồ sơ thí sinh khác qua id | Claude | Đạt | HTTP 200 |
| TC-SEC-04 | Bảo mật | Thí sinh xem học bạ thí sinh khác qua id | Claude | Đạt | HTTP 200 |
| TC-SEC-05 | Bảo mật | Thí sinh xuất học bạ thí sinh khác qua id: tệp không chứa dữ liệu người kia | Claude | Đạt | HTTP 200 |
| TC-SEC-06 | Bảo mật | Thí sinh bị chặn /quan-tri | Claude | Đạt |  |
| TC-SEC-06 | Bảo mật | Thí sinh bị chặn /giang-vien | Claude | Đạt |  |
| TC-SEC-06 | Bảo mật | Thí sinh bị chặn /bao-cao | Claude | Đạt |  |
| TC-SEC-06 | Bảo mật | Thí sinh bị chặn /quan-tri/nguoi-dung | Claude | Đạt |  |
| TC-SEC-06 | Bảo mật | Thí sinh bị chặn /api/v1/admin/users | Claude | Đạt |  |
| TC-CLN-01 | Dọn dẹp | Xoá tài khoản thử (nếu còn dữ liệu thì tài khoản chỉ bị khoá) | Claude | Đạt | còn lại: qa059d_gv, qa059d_ts1 |
| TC-SEC-07 | Bảo mật | Giới hạn đăng nhập sai liên tiếp → 429 | Claude | Không đạt | 24 lần sai liên tiếp không bị chặn vì mỗi lần đăng nhập mất vài giây; thử lại với 30 yêu cầu đồng thời vẫn 30/30 trả 200 (không có 429). Giới hạn theo IP không đủ khi đi qua nhiều lớp proxy của Render; lần kiểm thử trước đó có bị chặn. Cần khoá theo tài khoản. |
