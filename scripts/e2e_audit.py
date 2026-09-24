#!/usr/bin/env python3
"""Kiểm thử E2E QuizArena qua HTTP (không cần trình duyệt).

Dùng: python3 scripts/e2e_audit.py [BASE_URL]   (mặc định http://localhost:5299)
Yêu cầu: pip install requests. Chạy trên DB DEV/local đã seed — script tạo/xoá dữ liệu thử.
Ghi kết quả PASS/FAIL ra stdout và file docs/audit/E2E_RESULT.md.
"""
import re, sys, json, time, uuid, html as _html
import requests

# Razor mã hoá HTML ký tự tiếng Việt (&#x...;) nên giải mã trước khi so khớp.
requests.Response.utext = property(lambda self: _html.unescape(self.text))

BASE = (sys.argv[1] if len(sys.argv) > 1 else "http://localhost:5299").rstrip("/")
RESULTS = []

def check(group, name, ok, detail=""):
    RESULTS.append((group, name, bool(ok), detail))
    print(f"[{'PASS' if ok else 'FAIL'}] {group} :: {name} {detail}")

def token(html):
    m = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', html)
    return m.group(1) if m else None

class Client:
    def __init__(self):
        self.s = requests.Session()
    def get(self, path, **kw):
        return self.s.get(BASE + path, allow_redirects=kw.pop("allow_redirects", False), **kw)
    def post(self, path, data=None, page=None, **kw):
        data = dict(data or {})
        if "__RequestVerificationToken" not in data:
            html = self.get(page or "/dang-nhap", allow_redirects=True).utext
            data["__RequestVerificationToken"] = token(html) or ""
        return self.s.post(BASE + path, data=data, allow_redirects=kw.pop("allow_redirects", False), **kw)
    def login(self, u, p):
        r = self.post("/dang-nhap", {"Username": u, "Password": p}, page="/dang-nhap")
        return r

def login(u, p):
    c = Client(); r = c.login(u, p); return c, r

# ---------- 1. Công khai ----------
c = Client()
r = c.get("/"); check("Công khai", "Trang chủ 200", r.status_code == 200 and "QuizArena" in r.utext)
r = c.get("/dang-nhap"); check("Công khai", "Trang đăng nhập 200", r.status_code == 200)
check("Công khai", "Header CSP", "default-src 'self'" in r.headers.get("Content-Security-Policy", ""))
check("Công khai", "Header X-Frame-Options=DENY", r.headers.get("X-Frame-Options") == "DENY")
check("Công khai", "Header X-Content-Type-Options", r.headers.get("X-Content-Type-Options") == "nosniff")
r = c.get("/trang-thai"); check("Công khai", "Health /trang-thai", r.status_code == 200)
for old in ["/admin", "/teacher", "/student", "/account/login", "/api/exam/start/1"]:
    r = c.get(old); check("Đường dẫn", f"Link cũ {old} không còn", r.status_code in (404, 405), f"({r.status_code})")
for p in ["/quan-tri", "/giang-vien", "/thi-sinh", "/lich-thi", "/hoc-ba", "/bao-cao", "/thong-bao", "/api/v1/admin/users"]:
    r = c.get(p); check("Xác thực", f"Ẩn danh {p} bị chuyển/chặn", r.status_code in (302, 401), f"({r.status_code})")

# ---------- 2. Đăng nhập ----------
r = c.login("admin_root", "sai-mat-khau")
check("Đăng nhập", "Sai mật khẩu → báo lỗi, không đăng nhập", r.status_code == 200 and "Sai tên đăng nhập" in r.utext)
r = c.login("' OR 1=1 --", "x")
check("Đăng nhập", "SQL injection ở username bị từ chối", r.status_code == 200 and "Sai tên đăng nhập" in r.utext)
r = c.s.post(BASE + "/dang-nhap", data={"Username": "admin_root", "Password": "Admin@123456"}, allow_redirects=False)
check("CSRF", "POST đăng nhập không có token → 400", r.status_code == 400, f"({r.status_code})")

admin, r = login("admin_root", "Admin@123456")
check("Đăng nhập", "Admin → /quan-tri", r.status_code == 302 and r.headers["Location"] == "/quan-tri")
tk, rr = login("gv_tranvan", "Teacher@123")
check("Đăng nhập", "Giảng viên → /giang-vien", rr.headers.get("Location") == "/giang-vien")
st, rr = login("ts_nguyen01", "Student@123")
check("Đăng nhập", "Thí sinh → /thi-sinh", rr.headers.get("Location") == "/thi-sinh")
st2, rr = login("ts_lehoang", "Student@123")
check("Đăng nhập", "Thí sinh 2 → /thi-sinh", rr.headers.get("Location") == "/thi-sinh")

# ---------- 3. Phân quyền ----------
def code(cl, p): return cl.get(p).status_code
for p in ["/quan-tri", "/quan-tri/nguoi-dung", "/quan-tri/cai-dat", "/giang-vien", "/giang-vien/ky-thi/tao", "/bao-cao", "/api/v1/admin/users"]:
    check("Phân quyền", f"Thí sinh bị chặn {p}", code(st, p) in (302, 403, 404), f"({code(st, p)})")
for p in ["/quan-tri", "/quan-tri/nguoi-dung", "/api/v1/admin/users"]:
    check("Phân quyền", f"Giảng viên bị chặn {p}", code(tk, p) in (302, 403), f"({code(tk, p)})")
for p in ["/thi-sinh", "/api/v1/exam/time-remaining/" + str(uuid.uuid4())]:
    check("Phân quyền", f"Admin không vào khu thí sinh {p}", code(admin, p) in (302, 403), f"({code(admin, p)})")

# ---------- 4. Các trang chính (GET 200) ----------
PAGES = {
    admin: ["/quan-tri", "/quan-tri/lop-hoc", "/quan-tri/nam-hoc", "/quan-tri/phong-thi", "/quan-tri/nguoi-dung", "/quan-tri/giao-vien", "/quan-tri/mon-thi", "/quan-tri/cai-dat", "/bao-cao", "/thong-bao", "/lich-thi", "/quan-tri/lop-hoc/xuat", "/bao-cao/xuat", "/giang-vien"],
    tk: ["/giang-vien", "/giang-vien/ngan-hang-cau-hoi/cau-hoi-yeu", "/giang-vien/cau-truc-de", "/giang-vien/ky-thi/tao", "/giang-vien/ket-qua", "/giang-vien/phan-hoi-cau-hoi", "/lich-thi", "/ho-so", "/bao-cao", "/thong-bao"],
    st: ["/thi-sinh", "/lich-thi", "/ho-so", "/hoc-ba", "/hoc-ba//xuat".replace("//", "/"), "/thong-bao"],
}
for cl, paths in PAGES.items():
    for p in paths:
        r = cl.get(p)
        ok = r.status_code == 200 or (p.endswith("/xuat") and r.status_code in (200, 404))
        check("Trang chính", f"GET {p}", ok, f"({r.status_code})")

# ---------- 5. Quản trị: người dùng, môn thi ----------
uname = "e2e_" + uuid.uuid4().hex[:6]
r = admin.post("/quan-tri/nguoi-dung", {"username": uname, "email": uname + "@t.vn", "fullName": "E2E Tester", "password": "abc", "role": "Student"}, page="/quan-tri/nguoi-dung")
r2 = admin.get("/quan-tri/nguoi-dung")
check("Admin", "Mật khẩu < 6 ký tự bị từ chối", uname not in r2.utext)
r = admin.post("/quan-tri/nguoi-dung", {"username": uname, "email": uname + "@t.vn", "fullName": "E2E Tester", "password": "Abc12345", "role": "Student"}, page="/quan-tri/nguoi-dung")
r2 = admin.get("/quan-tri/nguoi-dung")
check("Admin", "Tạo người dùng thành công", uname in r2.utext)
m = re.search(r'/quan-tri/nguoi-dung/([0-9a-f-]{36})/xoa', r2.utext[r2.utext.index(uname) - 3000: r2.utext.index(uname) + 3000]) if uname in r2.utext else None
uid = m.group(1) if m else None
u2, rr = login(uname, "Abc12345"); check("Admin", "Tài khoản mới đăng nhập được", rr.headers.get("Location") == "/thi-sinh")
if uid:
    r = admin.post(f"/quan-tri/nguoi-dung/{uid}/bat-tat", page="/quan-tri/nguoi-dung")
    u3, rr = login(uname, "Abc12345"); check("Admin", "Khoá tài khoản → không đăng nhập được", rr.status_code == 200)
    admin.post(f"/quan-tri/nguoi-dung/{uid}/xoa", page="/quan-tri/nguoi-dung")
    check("Admin", "Xoá người dùng", uname not in admin.get("/quan-tri/nguoi-dung").utext)
subj = "Môn E2E " + uuid.uuid4().hex[:4]
admin.post("/quan-tri/mon-thi", {"name": subj, "description": "test"}, page="/quan-tri/mon-thi")
check("Admin", "Tạo môn thi", subj in admin.get("/quan-tri/mon-thi").utext)
# API
r = admin.get("/api/v1/admin/users"); j = r.json() if r.status_code == 200 else []
check("API", "GET /api/v1/admin/users 200", r.status_code == 200 and len(j) > 0)
check("API", "Không lộ passwordHash", "passwordHash" not in r.utext.lower())
r = admin.s.post(BASE + "/api/v1/admin/users", json={"username": "x", "email": "bad", "fullName": "", "password": "1", "role": 2})
check("API", "Tạo user dữ liệu sai → 400", r.status_code == 400, f"({r.status_code})")

# ---------- 6. Giảng viên: câu hỏi, kỳ thi ----------
html = tk.get("/giang-vien/ngan-hang-cau-hoi").utext
subjects = re.findall(r'<option value="(\d+)"[^>]*>([^<]+)</option>', html)
sid = subjects[0][0] if subjects else "1"
qtxt = "Câu E2E <script>alert(1)</script> " + uuid.uuid4().hex[:5]
tk.post("/giang-vien/ngan-hang-cau-hoi", {"subjectId": sid, "content": qtxt, "type": "SingleChoice", "difficulty": "Easy", "points": "1", "answers": "A\nB\nC\nD", "correct": "2"}, page="/giang-vien/ngan-hang-cau-hoi")
hr = tk.get("/giang-vien/ngan-hang-cau-hoi?q=E2E"); h = hr.utext
check("Giảng viên", "Thêm câu hỏi vào ngân hàng", "Câu E2E" in h)
check("Bảo mật", "XSS được mã hoá khi hiển thị", "<script>alert(1)</script>" not in hr.text and "&lt;script&gt;" in hr.text)
bad = "Câu sai luật " + uuid.uuid4().hex[:5]
tk.post("/giang-vien/ngan-hang-cau-hoi", {"subjectId": sid, "content": bad, "type": "SingleChoice", "difficulty": "Easy", "points": "1", "answers": "A\nB", "correct": "1,2"}, page="/giang-vien/ngan-hang-cau-hoi")
check("Giảng viên", "Câu 1 đáp án nhưng 2 đúng bị từ chối", bad not in tk.get("/giang-vien/ngan-hang-cau-hoi?q=sai+lu%E1%BA%ADt").utext)
for p in ["/giang-vien/ket-qua", "/giang-vien/ket-qua/1", "/giang-vien/ket-qua/1/xuat"]:
    check("Giảng viên", f"GET {p}", tk.get(p).status_code in (200, 404, 403), f"({tk.get(p).status_code})")

# Import CSV + xuất Excel
tag = uuid.uuid4().hex[:4]
csv_body = "content,type,difficulty,points,answers,correct\n" + "\n".join(
    f"Câu import {tag} {i},SingleChoice,Easy,1,A|B|C,2" for i in range(3)) + f"\nText import {tag},TextAnswer,Easy,1,paris,paris\n"
tok = token(tk.get("/giang-vien/ngan-hang-cau-hoi").text)
r = tk.s.post(BASE + "/giang-vien/ngan-hang-cau-hoi/nhap", data={"__RequestVerificationToken": tok, "fallbackSubjectId": sid},
              files={"file": ("q.csv", csv_body.encode("utf-8"), "text/csv")}, allow_redirects=False)
check("Giảng viên", "Import CSV 4 câu hỏi", "Câu import " + tag in tk.get("/giang-vien/ngan-hang-cau-hoi?q=" + tag).utext, f"({r.status_code})")
r = admin.get("/bao-cao/xuat")
check("Xuất file", "Báo cáo xuất .xlsx hợp lệ (zip)", r.status_code == 200 and r.content[:2] == b"PK", f"({r.status_code}, {len(r.content)}B)")
r = admin.get("/quan-tri/lop-hoc/xuat")
check("Xuất file", "Danh sách lớp xuất được", r.status_code == 200 and len(r.content) > 50, f"({r.status_code})")

# ---------- 7. Thí sinh: luồng thi ----------
dash = st.get("/thi-sinh").utext
exam_ids = re.findall(r'/thi-sinh/bat-dau/(\d+)', dash)
check("Thí sinh", "Có kỳ thi khả dụng trên bảng điều khiển", len(exam_ids) > 0, f"({len(exam_ids)} kỳ thi)")
attempt = None
if exam_ids:
    r = st.post(f"/thi-sinh/bat-dau/{exam_ids[0]}", page="/thi-sinh")
    loc = r.headers.get("Location", "")
    m = re.search(r'/thi-sinh/lam-bai/([0-9a-f-]{36})', loc)
    check("Thí sinh", "Bắt đầu thi → tạo lượt thi", bool(m), loc)
    if m: attempt = m.group(1)
if attempt:
    # Hai thí sinh cùng kỳ thi phải nhận đề khác nhau (ngẫu nhiên)
    r2 = st2.post(f"/thi-sinh/bat-dau/{exam_ids[0]}", page="/thi-sinh")
    m2 = re.search(r'/thi-sinh/lam-bai/([0-9a-f-]{36})', r2.headers.get("Location", ""))
    def paper(cl, att):
        out, i = [], 1
        while True:
            rq = cl.get(f"/api/v1/exam/question/{att}/{i}")
            if rq.status_code != 200: return out
            out.append(rq.json()["questionId"]); i += 1
    if m2:
        a1, a2 = paper(st, attempt), paper(st2, m2.group(1))
        check("Sinh đề", "Đề 2 thí sinh khác nhau (ngẫu nhiên câu/thứ tự)", a1 != a2 and len(a1) == len(a2) > 0, f"({len(a1)} câu)")
        check("Sinh đề", "Mỗi đề không trùng câu hỏi", len(set(a1)) == len(a1))
    page = st.get(f"/thi-sinh/lam-bai/{attempt}")
    check("Thí sinh", "Trang làm bài 200", page.status_code == 200)
    check("Bảo mật", "HTML bài thi không chứa cờ đáp án đúng", "isCorrect" not in page.utext.lower() and "IsCorrect" not in page.utext)
    check("Bảo mật", "Thí sinh khác không xem được bài", st2.get(f"/thi-sinh/lam-bai/{attempt}").status_code == 404)
    r = st.get(f"/api/v1/exam/question/{attempt}/1"); j = r.json() if r.status_code == 200 else {}
    check("API", "Lấy câu hỏi 200", r.status_code == 200)
    check("Bảo mật", "API câu hỏi KHÔNG lộ đáp án đúng", "iscorrect" not in r.utext.lower() and "explanation" not in r.utext.lower())
    qid = j.get("questionId"); choices = j.get("choices", [])
    hdr = {"Content-Type": "application/json"}
    body = {"attemptId": attempt, "questionId": qid, "answerIds": [choices[0]["id"]] if choices else None, "textInput": None if choices else "abc"}
    r = st.s.post(BASE + "/api/v1/exam/answer", json=body, headers=hdr); check("API", "Lưu đáp án → 204", r.status_code == 204, f"({r.status_code})")
    body["questionId"] = 999999
    r = st.s.post(BASE + "/api/v1/exam/answer", json=body, headers=hdr); check("Bảo mật", "Lưu đáp án cho câu ngoài đề → 409", r.status_code == 409, f"({r.status_code})")
    r = st2.s.post(BASE + "/api/v1/exam/answer", json={"attemptId": attempt, "questionId": qid, "answerIds": [1], "textInput": None}, headers=hdr)
    check("Bảo mật", "Lưu đáp án vào bài người khác bị từ chối", r.status_code in (409, 400, 403), f"({r.status_code})")
    r = st.get(f"/api/v1/exam/time-remaining/{attempt}"); check("API", "Đồng hồ còn lại > 0", r.status_code == 200 and r.json().get("remaining", 0) > 0, r.utext[:60])
    r = st.get(f"/thi-sinh/ket-qua/{attempt}"); check("Thí sinh", "Chưa nộp thì không xem kết quả", r.status_code == 404)
    r = st.post(f"/thi-sinh/nop-bai/{attempt}", page=f"/thi-sinh/lam-bai/{attempt}")
    check("Thí sinh", "Nộp bài → chuyển sang kết quả", "/thi-sinh/ket-qua/" in r.headers.get("Location", ""), r.headers.get("Location", ""))
    r = st.get(f"/thi-sinh/ket-qua/{attempt}"); check("Thí sinh", "Trang kết quả 200", r.status_code == 200)
    check("Bảo mật", "Thí sinh khác không xem kết quả", st2.get(f"/thi-sinh/ket-qua/{attempt}").status_code == 404)
    r = st.s.post(BASE + "/api/v1/exam/answer", json=body | {"questionId": qid}, headers=hdr); check("Thí sinh", "Không sửa đáp án sau khi nộp", r.status_code == 409, f"({r.status_code})")

# ---------- 8. Giới hạn tần suất đăng nhập (chạy cuối) ----------
codes = []
rl = Client()
for i in range(30):
    codes.append(rl.login("admin_root", "sai").status_code)
check("Bảo mật", "Rate-limit đăng nhập sai liên tiếp → 429", 429 in codes, f"(codes cuối: {codes[-3:]})")

passed = sum(1 for x in RESULTS if x[2]); total = len(RESULTS)
print(f"\nTỔNG: {passed}/{total} PASS")
import os
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "docs", "audit", "E2E_RESULT.md")
with open(OUT, "w", encoding="utf-8") as f:
    f.write(f"# Kết quả kiểm thử E2E\n\nMáy chủ: `{BASE}` · Thời điểm: {time.strftime('%Y-%m-%d %H:%M')}\n\n**{passed}/{total} PASS**\n\n| Nhóm | Kiểm tra | Kết quả |\n|---|---|---|\n")
    for g, n, ok, d in RESULTS:
        f.write(f"| {g} | {n} {d} | {'PASS' if ok else '**FAIL**'} |\n")
sys.exit(0 if passed == total else 1)
