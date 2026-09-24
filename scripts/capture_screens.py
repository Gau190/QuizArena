#!/usr/bin/env python3
"""Chụp ảnh các trang chính (Desktop + Mobile) cho báo cáo. Cần: pip install playwright && playwright install chromium.
Dùng: python3 scripts/capture_screens.py [BASE_URL] [OUT_DIR]"""
import sys
from playwright.sync_api import sync_playwright

BASE = (sys.argv[1] if len(sys.argv) > 1 else "http://localhost:5299").rstrip("/")
OUT = sys.argv[2] if len(sys.argv) > 2 else "screenshots"
VIEWPORTS = {"desktop": (1366, 800), "mobile": (390, 844)}

def login(pg, u, p):
    pg.goto(BASE + "/dang-nhap"); pg.fill("#Username", u); pg.fill("#login-password", p)
    pg.click("button.login-submit"); pg.wait_for_load_state("networkidle")

def shot(pg, name, vp, full=True):
    pg.wait_for_load_state("networkidle")
    pg.screenshot(path=f"{OUT}/{name}_{vp}.png", full_page=full)
    print("saved", name, vp)

with sync_playwright() as p:
    b = p.chromium.launch()
    for vp, (w, h) in VIEWPORTS.items():
        def new(): 
            c = b.new_context(viewport={"width": w, "height": h}); return c, c.new_page()
        c, pg = new(); pg.goto(BASE + "/"); shot(pg, "trang_chu", vp); pg.goto(BASE + "/dang-nhap"); shot(pg, "dang_nhap", vp); c.close()

        c, pg = new(); login(pg, "admin_root", "Admin@123456")
        for name, path in [("dashboard", "/quan-tri"), ("mon_thi", "/quan-tri/mon-thi"), ("nguoi_dung", "/quan-tri/nguoi-dung"), ("lop_hoc", "/quan-tri/lop-hoc")]:
            pg.goto(BASE + path); shot(pg, name, vp)
        if vp == "mobile":
            pg.goto(BASE + "/quan-tri"); pg.click("#nav-toggle"); pg.wait_for_timeout(400); shot(pg, "menu_mo", vp, full=False)
        c.close()

        c, pg = new(); login(pg, "gv_tranvan", "Teacher@123")
        for name, path in [("ma_tran_de", "/giang-vien/cau-truc-de"), ("cau_hoi", "/giang-vien/ngan-hang-cau-hoi"), ("bao_cao", "/bao-cao"), ("tao_ky_thi", "/giang-vien/ky-thi/tao")]:
            pg.goto(BASE + path); shot(pg, name, vp)
        c.close()

        c, pg = new(); login(pg, "ts_nguyen01", "Student@123")
        pg.goto(BASE + "/thi-sinh"); shot(pg, "thi_sinh", vp)
        pg.goto(BASE + "/hoc-ba"); shot(pg, "hoc_ba", vp)
        pg.goto(BASE + "/thi-sinh")
        pg.evaluate("document.querySelector('form[action*=\"bat-dau\"]').submit()"); pg.wait_for_load_state("networkidle")
        pg.wait_for_selector(".choice, #text-answer", timeout=8000)
        def answer_current():
            first = pg.query_selector(".choice")
            if first: first.click()
            elif pg.query_selector("#text-answer"): pg.fill("#text-answer", "2")
            pg.wait_for_timeout(400)
        answer_current(); pg.click("#btn-next"); pg.wait_for_timeout(500)
        answer_current(); pg.click("#btn-next"); pg.wait_for_timeout(500)
        pg.click("#mark-btn")            # đánh dấu câu 3, chưa trả lời
        pg.click("#btn-next"); pg.wait_for_timeout(500)
        answer_current()
        pg.wait_for_timeout(300); shot(pg, "lam_bai", vp)
        pg.evaluate("document.querySelector('form[action*=\"nop-bai\"]').submit()"); pg.wait_for_load_state("networkidle")
        shot(pg, "ket_qua", vp)
        c.close()
    b.close()
