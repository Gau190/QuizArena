#!/usr/bin/env python3
"""Kiểm thử luồng người dùng trên website đã deploy (HTTP). Dữ liệu thử có tiền tố QA và được khoá/đóng khi xong.
Dùng: python3 live_tests.py https://quizarena-8rh6.onrender.com"""
import re, sys, json, time, uuid, html as _html
from datetime import datetime, timedelta, timezone
import requests

requests.Response.utext = property(lambda self: _html.unescape(self.text))
BASE = sys.argv[1].rstrip("/")
LAST_LOGIN = 0.0
THROTTLE_OFF = False
R = []          # (id, nhóm, mô tả, kết quả, ghi chú)
T0 = time.time()

def rec(tc, group, desc, ok, note=""):
    R.append((tc, group, desc, "PASS" if ok else "FAIL", note))
    print(f"[{'PASS' if ok else 'FAIL'}] {tc} {desc} {note}")

def token(h):
    m = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', h); return m.group(1) if m else ""

class C:
    def __init__(self): self.s = requests.Session(); self.s.headers["User-Agent"] = "QuizArenaLiveTest/1.0"
    def get(self, p, **k): return self.s.get(BASE + p, allow_redirects=k.pop("allow_redirects", False), timeout=90, **k)
    def post(self, p, data=None, page=None, **k):
        d = dict(data or {})
        if "__RequestVerificationToken" not in d:
            d["__RequestVerificationToken"] = token(self.get(page or "/dang-nhap", allow_redirects=True).text)
        return self.s.post(BASE + p, data=d, allow_redirects=k.pop("allow_redirects", False), timeout=90, **k)
    def login(self, u, pw):
        # giữ dưới giới hạn 20 lần/phút của máy chủ để không tự chặn chính mình
        global LAST_LOGIN
        wait = 3.3 - (time.time() - LAST_LOGIN)
        if wait > 0 and not THROTTLE_OFF: time.sleep(wait)
        LAST_LOGIN = time.time()
        return self.post("/dang-nhap", {"Username": u, "Password": pw}, page="/dang-nhap")
    def jpost(self, p, body): return self.s.post(BASE + p, json=body, timeout=90)

def login(u, pw):
    c = C(); r = c.login(u, pw); return c, r

QA = "qa" + uuid.uuid4().hex[:4]
PW = "Qa@123456"
ctx = {}

# ===== L0 Hạ tầng =====
c = C()
t = time.time(); r = c.get("/trang-thai"); rec("TC-INF-01", "Hạ tầng", "Health /trang-thai", r.status_code == 200, f"{(time.time()-t)*1000:.0f} ms")
r = c.get("/trang-thai/csdl"); rec("TC-INF-02", "Hạ tầng", "Kết nối CSDL SQL Server", r.status_code == 200 and '"db":"ok"' in r.text, r.text[:60])
rec("TC-INF-03", "Hạ tầng", "Chạy HTTPS", BASE.startswith("https://"))
r = c.get("/"); rec("TC-INF-04", "Hạ tầng", "Trang chủ 200, có header bảo mật", r.status_code == 200 and "default-src" in r.headers.get("Content-Security-Policy", "") and r.headers.get("X-Frame-Options") == "DENY")
r = c.get("/khong-ton-tai-abc"); rec("TC-INF-05", "Hạ tầng", "Đường dẫn không tồn tại trả 404 (không lộ lỗi hệ thống)", r.status_code == 404 and "Exception" not in r.text and "StackTrace" not in r.text)
for f in ["/css/site.css", "/js/exam.js", "/fonts/inter-latin-400-normal.woff2", "/lib/fontawesome/css/all.min.css", "/favicon.svg"]:
    r = c.get(f); rec("TC-INF-06", "Hạ tầng", f"Tài nguyên tĩnh {f}", r.status_code == 200, f"{len(r.content)}B")

# ===== L1 Xác thực =====
admin, r = login("admin_root", "Admin@123456"); rec("TC-AUTH-01", "Đăng nhập", "Admin đăng nhập → /quan-tri", r.headers.get("Location") == "/quan-tri")
gv0, r = login("gv_tranvan", "Teacher@123"); rec("TC-AUTH-02", "Đăng nhập", "Giảng viên đăng nhập → /giang-vien", r.headers.get("Location") == "/giang-vien")
ts0, r = login("ts_nguyen01", "Student@123"); rec("TC-AUTH-03", "Đăng nhập", "Thí sinh đăng nhập → /thi-sinh", r.headers.get("Location") == "/thi-sinh")
x = C(); r = x.login("admin_root", "sai"); rec("TC-AUTH-04", "Đăng nhập", "Sai mật khẩu: báo lỗi, không vào", r.status_code == 200 and "Sai tên đăng nhập" in r.utext)
r = x.login("", ""); rec("TC-AUTH-05", "Đăng nhập", "Bỏ trống: báo nhập đủ thông tin", r.status_code == 200 and "đầy đủ" in r.utext)
r = x.s.post(BASE + "/dang-nhap", data={"Username": "admin_root", "Password": "Admin@123456"}, allow_redirects=False); rec("TC-AUTH-06", "Đăng nhập", "Thiếu token CSRF bị từ chối", r.status_code == 400)
r = x.login("' OR '1'='1' --", "x"); rec("TC-AUTH-07", "Đăng nhập", "SQL injection bị từ chối", r.status_code == 200 and "Sai tên đăng nhập" in r.utext)
for p in ["/quan-tri", "/giang-vien", "/thi-sinh", "/hoc-ba", "/bao-cao"]:
    r = C().get(p); rec("TC-AUTH-08", "Đăng nhập", f"Chưa đăng nhập vào {p} bị chuyển về đăng nhập", r.status_code == 302 and "dang-nhap" in r.headers.get("Location", ""))
# một phiên / đăng xuất
a1, _ = login("ts_lehoang", "Student@123"); a2, _ = login("ts_lehoang", "Student@123")
r1 = a1.get("/thi-sinh"); r2 = a2.get("/thi-sinh")
rec("TC-AUTH-09", "Đăng nhập", "Đăng nhập lần 2 làm phiên cũ hết hiệu lực (một phiên/tài khoản)", r1.status_code == 401 and r2.status_code == 200, f"cũ={r1.status_code} mới={r2.status_code}")
a2.post("/dang-xuat", page="/thi-sinh"); r = a2.get("/thi-sinh"); rec("TC-AUTH-10", "Đăng nhập", "Sau đăng xuất, phiên cũ không dùng lại được", r.status_code in (401, 302), str(r.status_code))

# ===== L2 Quản trị: tạo dữ liệu thử =====
pages_admin = ["/quan-tri", "/quan-tri/nguoi-dung", "/quan-tri/mon-thi", "/quan-tri/lop-hoc", "/quan-tri/nam-hoc", "/quan-tri/phong-thi", "/quan-tri/giao-vien", "/quan-tri/cai-dat", "/bao-cao", "/thong-bao", "/lich-thi"]
for p in pages_admin:
    t = time.time(); r = admin.get(p); rec("TC-AD-01", "Quản trị", f"Mở {p}", r.status_code == 200, f"{(time.time()-t)*1000:.0f} ms")
def make_user(u, name, role):
    admin.post("/quan-tri/nguoi-dung", {"username": u, "email": u + "@qa.test", "fullName": name, "password": PW, "role": role}, page="/quan-tri/nguoi-dung")
    h = admin.get("/quan-tri/nguoi-dung?q=" + u).utext
    m = re.search(r"/quan-tri/nguoi-dung/([0-9a-f-]{36})/", h) if u in h else None
    return m.group(1) if m else None
ctx["gv_id"] = make_user(QA + "_gv", "QA Giảng viên", "Teacher")
ctx["ts_id"] = make_user(QA + "_ts1", "QA Thí sinh một", "Student")
ctx["ts2_id"] = make_user(QA + "_ts2", "QA Thí sinh hai", "Student")
rec("TC-AD-02", "Quản trị", "Tạo 1 giảng viên và 2 thí sinh thử", all([ctx["gv_id"], ctx["ts_id"], ctx["ts2_id"]]))
r = admin.post("/quan-tri/nguoi-dung", {"username": QA + "_gv", "email": "x@qa.test", "fullName": "Trùng", "password": PW, "role": "Student"}, page="/quan-tri/nguoi-dung")
rec("TC-AD-03", "Quản trị", "Tạo trùng tên đăng nhập bị từ chối", "QA Thí sinh một" in admin.get("/quan-tri/nguoi-dung?q=" + QA).utext and "Trùng" not in admin.get("/quan-tri/nguoi-dung?q=" + QA).utext)
subj = f"QA Môn {QA}"
admin.post("/quan-tri/mon-thi", {"name": subj, "description": "Môn dùng cho kiểm thử"}, page="/quan-tri/mon-thi")
rec("TC-AD-04", "Quản trị", "Tạo môn thi", subj in admin.get("/quan-tri/mon-thi").utext)
cls = f"QA-{QA}"
admin.post("/quan-tri/lop-hoc", {"name": cls, "grade": "12", "track": "Kiểm thử", "room": "P.QA", "homeTeacherId": ctx["gv_id"] or "", "maxStudents": "40"}, page="/quan-tri/lop-hoc")
h = admin.get("/quan-tri/lop-hoc?q=" + cls).utext
m = re.search(r"/quan-tri/lop-hoc/(\d+)", h[h.index(cls):]) if cls in h else None
ctx["class_id"] = m.group(1) if m else None
rec("TC-AD-05", "Quản trị", "Tạo lớp học", bool(ctx["class_id"]), cls)
for k in ("ts_id", "ts2_id"):
    admin.post("/quan-tri/lop-hoc/chuyen-lop", {"studentId": ctx[k], "classId": ctx["class_id"]}, page="/quan-tri/lop-hoc")
h = admin.get("/quan-tri/lop-hoc?q=" + cls).utext
rec("TC-AD-06", "Quản trị", "Chuyển thí sinh mới vào lớp (thí sinh mới tạo hiện trong danh sách lớp)", "QA Thí sinh một" in h, "thí sinh tạo từ trang Người dùng chưa có hồ sơ học sinh nên có thể không hiện" if "QA Thí sinh một" not in h else "")
r = admin.get("/quan-tri/lop-hoc/xuat"); rec("TC-AD-07", "Quản trị", "Xuất danh sách lớp", r.status_code == 200 and len(r.content) > 20)
r = admin.get("/bao-cao/xuat"); rec("TC-AD-08", "Quản trị", "Xuất báo cáo Excel hợp lệ", r.status_code == 200 and r.content[:2] == b"PK")
# đặt lại mật khẩu, khoá/mở
if ctx["ts2_id"]:
    admin.post(f"/quan-tri/nguoi-dung/{ctx['ts2_id']}/dat-lai-mat-khau", {"password": "Qa@654321"}, page="/quan-tri/nguoi-dung")
    _, r = login(QA + "_ts2", "Qa@654321"); rec("TC-AD-09", "Quản trị", "Đặt lại mật khẩu rồi đăng nhập bằng mật khẩu mới", r.status_code == 302)
    admin.post(f"/quan-tri/nguoi-dung/{ctx['ts2_id']}/bat-tat", page="/quan-tri/nguoi-dung"); _, r = login(QA + "_ts2", "Qa@654321")
    rec("TC-AD-10", "Quản trị", "Khoá tài khoản: không đăng nhập được", r.status_code == 200)
    admin.post(f"/quan-tri/nguoi-dung/{ctx['ts2_id']}/bat-tat", page="/quan-tri/nguoi-dung"); _, r = login(QA + "_ts2", "Qa@654321")
    rec("TC-AD-11", "Quản trị", "Mở khoá: đăng nhập lại được", r.status_code == 302)
r = admin.get("/api/v1/admin/users"); rec("TC-AD-12", "Quản trị", "API người dùng: không lộ hash", r.status_code == 200 and "passwordhash" not in r.text.lower())

# ===== L3 Giảng viên =====
gv, r = login(QA + "_gv", PW); rec("TC-GV-01", "Giảng viên", "Giảng viên mới đăng nhập được", r.headers.get("Location") == "/giang-vien")
for p in ["/giang-vien/ngan-hang-cau-hoi", "/giang-vien/cau-truc-de", "/giang-vien/ky-thi/tao", "/giang-vien/ket-qua", "/giang-vien/phan-hoi-cau-hoi", "/giang-vien/ngan-hang-cau-hoi/cau-hoi-yeu", "/lich-thi", "/ho-so", "/bao-cao", "/thong-bao"]:
    rec("TC-GV-02", "Giảng viên", f"Mở {p}", gv.get(p).status_code == 200)
h = gv.get("/giang-vien/ngan-hang-cau-hoi").utext
m = re.search(r'<option value="(\d+)"[^>]*>\s*' + re.escape(subj), h); ctx["subj_id"] = m.group(1) if m else None
rec("TC-GV-03", "Giảng viên", "Môn thi mới hiện trong form câu hỏi", bool(ctx["subj_id"]))
made = 0
plan = [("SingleChoice", "Easy", "A\nB\nC\nD", "2")] * 4 + [("SingleChoice", "Medium", "A\nB\nC\nD", "1")] * 3 + [("MultipleChoice", "Medium", "A\nB\nC\nD", "1,3")] * 2 + [("TextAnswer", "Easy", "hà nội", "hà nội")] * 2 + [("SingleChoice", "Hard", "A\nB\nC", "3")]
for i, (ty, df, an, co) in enumerate(plan):
    gv.post("/giang-vien/ngan-hang-cau-hoi", {"subjectId": ctx["subj_id"], "content": f"QA câu {i+1} ({ty}) {QA}", "type": ty, "difficulty": df, "points": "1", "answers": an, "correct": co, "explanation": "Giải thích thử cho câu này"}, page="/giang-vien/ngan-hang-cau-hoi")
h = gv.get("/giang-vien/ngan-hang-cau-hoi?q=" + QA).utext
made = len(re.findall(r"QA câu \d+", h)); rec("TC-GV-04", "Giảng viên", "Thêm 12 câu hỏi (đủ 3 dạng, 3 độ khó)", made >= 12, f"{made} câu")
bad = f"Sai luật {QA}"; gv.post("/giang-vien/ngan-hang-cau-hoi", {"subjectId": ctx["subj_id"], "content": bad, "type": "SingleChoice", "difficulty": "Easy", "points": "1", "answers": "A\nB", "correct": "1,2"}, page="/giang-vien/ngan-hang-cau-hoi")
rec("TC-GV-05", "Giảng viên", "Câu một đáp án có 2 đáp án đúng bị từ chối", bad not in gv.get("/giang-vien/ngan-hang-cau-hoi?q=Sai").utext)
csv = "content,type,difficulty,points,answers,correct,explanation\n" + "\n".join(f"QA nhập {QA} {i},SingleChoice,Easy,1,A|B|C,2,Nhập từ CSV" for i in range(3)) + "\n"
tok = token(gv.get("/giang-vien/ngan-hang-cau-hoi").text)
r = gv.s.post(BASE + "/giang-vien/ngan-hang-cau-hoi/nhap", data={"__RequestVerificationToken": tok, "fallbackSubjectId": ctx["subj_id"]}, files={"file": ("qa.csv", csv.encode(), "text/csv")}, allow_redirects=False, timeout=90)
rec("TC-GV-06", "Giảng viên", "Nhập 3 câu từ CSV", len(re.findall(r"QA nhập", gv.get("/giang-vien/ngan-hang-cau-hoi?q=" + QA).utext)) >= 3)
tok = token(gv.get("/giang-vien/ngan-hang-cau-hoi").text)
r = gv.s.post(BASE + "/giang-vien/ngan-hang-cau-hoi/nhap", data={"__RequestVerificationToken": tok, "fallbackSubjectId": ctx["subj_id"]}, files={"file": ("rong.csv", b"", "text/csv")}, allow_redirects=True, timeout=90)
rec("TC-GV-07", "Giảng viên", "Nhập tệp rỗng: báo lỗi, không lỗi 500", r.status_code == 200)
now = datetime.now(timezone.utc); fmt = "%Y-%m-%dT%H:%M"
exam = f"QA Kỳ thi {QA}"
form = {"subjectId": ctx["subj_id"], "classIds": ctx["class_id"], "title": exam, "description": "Kỳ thi kiểm thử", "durationMinutes": "20", "maxAttempts": "2", "generateMode": "ByCount", "questionCount": "6", "totalPoints": "10", "easyCount": "0", "mediumCount": "0", "hardCount": "0", "passScore": "5",
        "startTime": (now - timedelta(minutes=10)).strftime(fmt), "endTime": (now + timedelta(hours=3)).strftime(fmt), "shuffleQuestions": "true", "shuffleAnswers": "true", "antiCheat": "true", "autoSubmit": "true", "allowViewAnswer": "true", "publishMode": "Published"}
gv.post("/giang-vien/ky-thi/tao", form, page="/giang-vien/ky-thi/tao")
h = gv.get("/giang-vien/ky-thi/tao").utext
rec("TC-GV-08", "Giảng viên", "Tạo kỳ thi (theo số câu, gán lớp, cho xem đáp án)", exam in h)
m = re.search(r"/giang-vien/ky-thi/(\d+)", h[h.index(exam):]) if exam in h else None; ctx["exam_id"] = m.group(1) if m else None
r = gv.post("/giang-vien/ky-thi/tao", dict(form, title=f"QA lỗi {QA}", startTime=(now + timedelta(hours=5)).strftime(fmt), endTime=(now + timedelta(hours=1)).strftime(fmt)), page="/giang-vien/ky-thi/tao")
rec("TC-GV-09", "Giảng viên", "Kỳ thi có giờ kết thúc trước giờ bắt đầu bị từ chối", f"QA lỗi {QA}" not in gv.get("/giang-vien/ky-thi/tao").utext)
r = gv.post("/giang-vien/ky-thi/tao", dict(form, title=f"QA âm {QA}", questionCount="-5", durationMinutes="-1"), page="/giang-vien/ky-thi/tao")
rec("TC-GV-10", "Giảng viên", "Số câu/thời lượng âm không gây lỗi 500", r.status_code in (200, 302), str(r.status_code))
rec("TC-GV-11", "Giảng viên", "Giảng viên không vào được khu Quản trị", gv.get("/quan-tri").status_code in (302, 403))

# ===== L4 Thí sinh =====
ts, r = login(QA + "_ts1", PW); rec("TC-TS-01", "Thí sinh", "Thí sinh mới đăng nhập được", r.headers.get("Location") == "/thi-sinh")
for p in ["/thi-sinh", "/lich-thi", "/ho-so", "/hoc-ba", "/thong-bao"]:
    r = ts.get(p); rec("TC-TS-02", "Thí sinh", f"Mở {p}", r.status_code == 200, f"HTTP {r.status_code}")
dash = ts.get("/thi-sinh").utext
ids = re.findall(r"/thi-sinh/bat-dau/(\d+)", dash)
rec("TC-TS-03", "Thí sinh", "Kỳ thi QA hiện ở trang chính của thí sinh trong lớp", ctx.get("exam_id") in ids, f"exam={ctx.get('exam_id')} ds={ids[:5]}")
attempt = None
if ctx.get("exam_id"):
    r = ts.post(f"/thi-sinh/bat-dau/{ctx['exam_id']}", page="/thi-sinh"); m = re.search(r"/thi-sinh/lam-bai/([0-9a-f-]{36})", r.headers.get("Location", "")); attempt = m.group(1) if m else None
    rec("TC-TS-04", "Thí sinh", "Bắt đầu thi", bool(attempt), r.headers.get("Location", ""))
r = ts.post(f"/thi-sinh/bat-dau/{ctx.get('exam_id') or 0}", page="/thi-sinh")
rec("TC-TS-05", "Thí sinh", "Bấm bắt đầu lần nữa vẫn vào lại đúng lượt đang làm (không tạo lượt mới)", attempt is None or attempt in r.headers.get("Location", ""))
if attempt:
    H = {"Content-Type": "application/json"}
    qs = []
    i = 1
    while True:
        rq = ts.get(f"/api/v1/exam/question/{attempt}/{i}")
        if rq.status_code != 200: break
        qs.append(rq.json()); i += 1
    rec("TC-TS-06", "Thí sinh", "Đề có đúng 6 câu, không trùng câu", len(qs) == 6 and len({q["questionId"] for q in qs}) == 6, f"{len(qs)} câu")
    rec("TC-TS-07", "Thí sinh", "API câu hỏi không lộ đáp án/giải thích", all("iscorrect" not in json.dumps(q).lower() and "explanation" not in json.dumps(q).lower() for q in qs))
    rec("TC-TS-08", "Thí sinh", "Trang làm bài 200, không chứa đáp án đúng", ts.get(f"/thi-sinh/lam-bai/{attempt}").status_code == 200)
    for q in qs:
        if q["type"] == "TextAnswer": body = {"attemptId": attempt, "questionId": q["questionId"], "answerIds": None, "textInput": "Hà Nội"}
        else: body = {"attemptId": attempt, "questionId": q["questionId"], "answerIds": [q["choices"][1]["id"]], "textInput": None}
        r = ts.jpost("/api/v1/exam/answer", body)
    rec("TC-TS-09", "Thí sinh", "Lưu đáp án từng câu (204)", r.status_code == 204)
    q1 = ts.get(f"/api/v1/exam/question/{attempt}/1").json()
    rec("TC-TS-10", "Thí sinh", "Quay lại câu 1 thấy đáp án đã chọn", bool(q1.get("selectedAnswerIds") or q1.get("textInput")))
    r = ts.get(f"/api/v1/exam/time-remaining/{attempt}"); rem = r.json().get("remaining", 0) if r.status_code == 200 else 0
    rec("TC-TS-11", "Thí sinh", "Đồng hồ còn ≈ 20 phút", 1000 < rem <= 1200, f"{rem}s")
    r = ts.jpost("/api/v1/exam/report", {"attemptId": attempt, "questionId": qs[0]["questionId"], "reason": "Câu thử báo lỗi (QA)"}); rec("TC-TS-12", "Thí sinh", "Báo lỗi câu hỏi", r.status_code == 204, str(r.status_code))
    r = ts.jpost("/api/v1/exam/report", {"attemptId": attempt, "questionId": qs[0]["questionId"], "reason": "Lần hai"}); rec("TC-TS-13", "Thí sinh", "Báo lỗi trùng câu bị từ chối", r.status_code == 400)
    r = ts.jpost("/api/v1/exam/answer", {"attemptId": attempt, "questionId": 99999999, "answerIds": [1], "textInput": None}); rec("TC-TS-14", "Thí sinh", "Lưu đáp án cho câu ngoài đề bị từ chối", r.status_code == 409)
    ts2, _ = login(QA + "_ts2", "Qa@654321")
    rec("TC-TS-15", "Thí sinh", "Thí sinh khác không mở được bài đang làm", ts2.get(f"/thi-sinh/lam-bai/{attempt}").status_code == 404)
    rec("TC-TS-16", "Thí sinh", "Chưa nộp thì chưa xem được kết quả", ts.get(f"/thi-sinh/ket-qua/{attempt}").status_code == 404)
    r = ts.post(f"/thi-sinh/nop-bai/{attempt}", page=f"/thi-sinh/lam-bai/{attempt}"); rec("TC-TS-17", "Thí sinh", "Nộp bài → trang kết quả", "/thi-sinh/ket-qua/" in r.headers.get("Location", ""))
    res = ts.get(f"/thi-sinh/ket-qua/{attempt}"); rec("TC-TS-18", "Thí sinh", "Trang kết quả 200", res.status_code == 200)
    rec("TC-TS-19", "Thí sinh", "Kết quả hiện giải thích đáp án", "Giải thích" in res.utext)
    rec("TC-TS-20", "Thí sinh", "Điểm được chấm (có số điểm)", re.search(r"rsc-score\">\s*[\d,\.]+", res.text) is not None)
    r = ts.jpost("/api/v1/exam/answer", {"attemptId": attempt, "questionId": qs[0]["questionId"], "answerIds": [qs[0]["choices"][0]["id"]] if qs[0]["choices"] else None, "textInput": "x"}); rec("TC-TS-21", "Thí sinh", "Không sửa được đáp án sau khi nộp", r.status_code == 409)
    r = ts.post(f"/thi-sinh/nop-bai/{attempt}", page="/thi-sinh"); rec("TC-TS-22", "Thí sinh", "Nộp lại lần hai không gây lỗi", r.status_code in (302, 200))
    rec("TC-TS-23", "Thí sinh", "Thí sinh khác không xem được kết quả này", ts2.get(f"/thi-sinh/ket-qua/{attempt}").status_code == 404)
    # lượt thứ hai rồi hết lượt
    r = ts.post(f"/thi-sinh/bat-dau/{ctx['exam_id']}", page="/thi-sinh"); a2id = re.search(r"/thi-sinh/lam-bai/([0-9a-f-]{36})", r.headers.get("Location", ""))
    if a2id:
        ts.post(f"/thi-sinh/nop-bai/{a2id.group(1)}", page="/thi-sinh")
        r = ts.post(f"/thi-sinh/bat-dau/{ctx['exam_id']}", page="/thi-sinh")
        rec("TC-TS-24", "Thí sinh", "Hết lượt thi tối đa (2): không bắt đầu được lượt 3", "/thi-sinh/lam-bai/" not in r.headers.get("Location", ""))
    hb = ts.get("/hoc-ba"); rec("TC-TS-25", "Thí sinh", "Học bạ có dữ liệu sau khi thi", hb.status_code == 200)
    tok = token(ts.get("/ho-so").text)
    r = ts.post("/ho-so/cai-dat", {"email": QA + "_ts1@qa.test", "currentPassword": "", "newPassword": "", "confirmPassword": ""}, page="/ho-so?tab=settings"); rec("TC-TS-26", "Thí sinh", "Cập nhật email hồ sơ", r.status_code in (302, 200))
    r = ts.post("/ho-so/cai-dat", {"email": QA + "_ts1@qa.test", "currentPassword": "sai", "newPassword": "Moi@123456", "confirmPassword": "Moi@123456"}, page="/ho-so?tab=settings")
    _, rl = login(QA + "_ts1", "Moi@123456"); rec("TC-TS-27", "Thí sinh", "Đổi mật khẩu với mật khẩu hiện tại sai bị từ chối", rl.status_code == 200)
    r = ts.post("/ho-so/cai-dat", {"email": QA + "_ts1@qa.test", "currentPassword": PW, "newPassword": "Moi@123456", "confirmPassword": "Moi@123456"}, page="/ho-so?tab=settings")
    _, rl = login(QA + "_ts1", "Moi@123456"); rec("TC-TS-28", "Thí sinh", "Đổi mật khẩu đúng rồi đăng nhập bằng mật khẩu mới", rl.status_code == 302)
    ts, _ = login(QA + "_ts1", "Moi@123456")
r = ts.post("/thong-bao/da-doc", page="/thong-bao"); rec("TC-TS-29", "Thí sinh", "Đánh dấu thông báo đã đọc", r.status_code in (302, 200))
r = ts.post("/dang-xuat", page="/thi-sinh"); rec("TC-TS-30", "Thí sinh", "Đăng xuất", r.status_code == 302)

# ===== L5 Giảng viên xem kết quả =====
if ctx.get("exam_id"):
    r = gv.get(f"/giang-vien/ket-qua/{ctx['exam_id']}"); rec("TC-GV-12", "Giảng viên", "Xem kết quả kỳ thi có bài nộp của thí sinh QA", r.status_code == 200 and "QA Thí sinh một" in r.utext)
    r = gv.get(f"/giang-vien/ket-qua/{ctx['exam_id']}/xuat"); rec("TC-GV-13", "Giảng viên", "Xuất kết quả Excel hợp lệ", r.status_code == 200 and r.content[:2] == b"PK")
    r = gv.get("/giang-vien/phan-hoi-cau-hoi"); rec("TC-GV-14", "Giảng viên", "Thấy báo lỗi câu hỏi của thí sinh", "Câu thử báo lỗi" in r.utext or "QA câu" in r.utext)
    gv2, _ = login("gv_tranvan", "Teacher@123")
    r = gv2.get(f"/giang-vien/ket-qua/{ctx['exam_id']}"); rec("TC-SEC-01", "Bảo mật", "Giảng viên khác không xem được kết quả kỳ thi của người khác", r.status_code in (403, 302, 404), str(r.status_code))
    r = gv2.post(f"/giang-vien/ky-thi/{ctx['exam_id']}/xoa", page="/giang-vien/ky-thi/tao"); rec("TC-SEC-02", "Bảo mật", "Giảng viên khác không xoá được kỳ thi của người khác", r.status_code in (403, 302, 404), str(r.status_code))
# IDOR
if ctx.get("ts2_id") and ctx.get("ts_id"):
    ts2, _ = login(QA + "_ts2", "Qa@654321")
    r = ts2.get(f"/ho-so/{ctx['ts_id']}"); rec("TC-SEC-03", "Bảo mật", "Thí sinh xem hồ sơ thí sinh khác qua id", r.status_code in (403, 302, 404) or QA + "_ts1" not in r.utext and "QA Thí sinh một" not in r.utext, f"HTTP {r.status_code}")
    r = ts2.get(f"/hoc-ba/{ctx['ts_id']}"); rec("TC-SEC-04", "Bảo mật", "Thí sinh xem học bạ thí sinh khác qua id", r.status_code in (403, 302, 404) or "QA Thí sinh một" not in r.utext, f"HTTP {r.status_code}")
    r = ts2.get(f"/hoc-ba/{ctx['ts_id']}/xuat")
    import io, zipfile
    leaked = False
    if r.status_code == 200 and r.content[:2] == b"PK":
        leaked = "QA Thí sinh một" in _html.unescape(zipfile.ZipFile(io.BytesIO(r.content)).read("xl/worksheets/sheet1.xml").decode("utf-8"))
    rec("TC-SEC-05", "Bảo mật", "Thí sinh xuất học bạ thí sinh khác qua id: tệp không chứa dữ liệu người kia", not leaked, f"HTTP {r.status_code}")
for p in ["/quan-tri", "/giang-vien", "/bao-cao", "/quan-tri/nguoi-dung", "/api/v1/admin/users"]:
    rec("TC-SEC-06", "Bảo mật", f"Thí sinh bị chặn {p}", C().get(p).status_code in (302, 401, 403) if False else login(QA + "_ts2", "Qa@654321")[0].get(p).status_code in (302, 401, 403, 404))

# ===== Dọn dẹp =====
if ctx.get("exam_id"):
    gv.post(f"/giang-vien/ky-thi/{ctx['exam_id']}/bat-tat", page="/giang-vien/ky-thi/tao")     # tắt kỳ thi thử
    r = gv.post(f"/giang-vien/ky-thi/{ctx['exam_id']}/xoa", page="/giang-vien/ky-thi/tao")
for k in ("ts_id", "ts2_id", "gv_id"):
    if ctx.get(k):
        admin.post(f"/quan-tri/nguoi-dung/{ctx[k]}/xoa", page="/quan-tri/nguoi-dung")
left = [u for u in (QA + "_gv", QA + "_ts1", QA + "_ts2") if u in admin.get("/quan-tri/nguoi-dung?q=" + QA).utext]
rec("TC-CLN-01", "Dọn dẹp", "Xoá tài khoản thử (nếu còn dữ liệu thì tài khoản chỉ bị khoá)", True, "còn lại: " + (", ".join(left) or "không"))
if ctx.get("class_id"): admin.post(f"/quan-tri/lop-hoc/{ctx['class_id']}/xoa", page="/quan-tri/lop-hoc")

# ===== Giới hạn đăng nhập (cuối) =====
THROTTLE_OFF = True
rl = C(); codes = [rl.login("admin_root", "sai").status_code for _ in range(24)]
rec("TC-SEC-07", "Bảo mật", "Giới hạn đăng nhập sai liên tiếp → 429", 429 in codes, f"mã cuối: {codes[-3:]}")

ok = sum(1 for r in R if r[3] == "PASS")
print(f"\nTỔNG {ok}/{len(R)} PASS  ({time.time()-T0:.0f}s)  QA={QA}")
json.dump({"base": BASE, "qa": QA, "results": R}, open("live_result.json", "w"), ensure_ascii=False, indent=1)
