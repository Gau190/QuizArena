// Phòng thi: chuyển câu, lưu đáp án và đồng hồ chạy bằng fetch — trang không tải lại.
(() => {
  const root = document.getElementById("exam-root");
  if (!root) return;

  const attemptId = root.dataset.attemptId;
  const total = Number(root.dataset.total);
  const api = "/api/v1/exam";
  const headers = { "Content-Type": "application/json" };
  const TYPE_LABEL = { SingleChoice: "Một đáp án", MultipleChoice: "Nhiều đáp án", TextAnswer: "Điền đáp án" };

  const el = id => document.getElementById(id);
  const timer = el("timer-display"), warning = el("anti-cheat");
  const answered = new Set(root.dataset.answered.split(",").filter(Boolean).map(Number));
  const flagKey = "quizarena.flag." + attemptId;
  let flagged = new Set();
  try { flagged = new Set(JSON.parse(localStorage.getItem(flagKey) || "[]")); } catch { /* localStorage không khả dụng */ }

  let current = null;          // dữ liệu câu đang hiển thị
  let remainingSeconds = 0;
  let tabSwitchCount = 0;
  let saveTimer = null;

  // ---------- Đồng hồ ----------
  function renderTimer() {
    const s = Math.max(0, remainingSeconds);
    timer.textContent = `${String(Math.floor(s / 60)).padStart(2, "0")}:${String(s % 60).padStart(2, "0")}`;
    timer.classList.toggle("warn", s <= 300 && s > 60);
    timer.classList.toggle("low", s <= 60);
  }
  async function syncTimer() {
    const res = await fetch(`${api}/time-remaining/${attemptId}`, { headers });
    if (!res.ok) return;
    const data = await res.json();
    remainingSeconds = data.remaining || 0;
    renderTimer();
    if (data.autoSubmitted) location.href = `/thi-sinh/ket-qua/${attemptId}`;
  }

  // ---------- Bảng câu hỏi ----------
  function renderPalette() {
    const grid = el("palette");
    grid.replaceChildren();
    for (let i = 1; i <= total; i++) {
      const b = document.createElement("button");
      b.type = "button";
      b.className = "qn" + (i === current?.index ? " current" : "") + (answered.has(i) ? " answered" : "") + (flagged.has(i) ? " flagged" : "");
      b.textContent = i;
      b.setAttribute("aria-label", `Câu ${i}${answered.has(i) ? ", đã làm" : ""}${flagged.has(i) ? ", đã đánh dấu" : ""}`);
      b.addEventListener("click", () => go(i));
      grid.appendChild(b);
    }
    el("answered-count").textContent = answered.size;
    el("progress-bar").style.width = Math.round(answered.size * 100 / Math.max(total, 1)) + "%";
    const mark = el("mark-btn");
    const on = flagged.has(current?.index);
    mark.innerHTML = `<i class="fa-${on ? "solid" : "regular"} fa-flag"></i> ${on ? "Bỏ đánh dấu" : "Đánh dấu"}`;
  }

  // ---------- Hiển thị câu hỏi (dựng DOM bằng textContent để tránh XSS) ----------
  function renderQuestion(q) {
    current = q;
    el("q-count").textContent = `Câu ${q.index} / ${total}`;
    const meta = el("q-meta"); meta.replaceChildren();
    const n = document.createElement("span"); n.textContent = `Câu ${q.index}`; n.style.fontWeight = "600"; n.style.color = "var(--text-primary)";
    const t = document.createElement("span"); t.className = "tag"; t.textContent = TYPE_LABEL[q.type] || q.type;
    const p = document.createElement("span"); p.textContent = `${Number(q.points).toString().replace(".", ",")} điểm`;
    meta.append(n, t, p);
    el("q-text").textContent = q.content;

    const box = el("q-answers"); box.replaceChildren();
    if (q.type === "TextAnswer") {
      const wrap = document.createElement("label"); wrap.className = "field text-answer";
      const inp = document.createElement("input"); inp.id = "text-answer"; inp.autocomplete = "off";
      inp.placeholder = "Nhập câu trả lời"; inp.value = q.textInput || "";
      inp.addEventListener("input", queueSave);
      wrap.appendChild(inp); box.appendChild(wrap);
    } else {
      const multi = q.type === "MultipleChoice";
      const selected = new Set(q.selectedAnswerIds ? JSON.parse(q.selectedAnswerIds) : []);
      const list = document.createElement("div"); list.className = "choice-list";
      q.choices.forEach((c, i) => {
        const label = document.createElement("label");
        label.className = "choice" + (multi ? " multi" : "") + (selected.has(c.id) ? " selected" : "");
        const input = document.createElement("input");
        input.type = multi ? "checkbox" : "radio"; input.name = "answerIds"; input.value = c.id; input.checked = selected.has(c.id);
        const marker = document.createElement("span"); marker.className = "marker"; marker.innerHTML = '<i class="fa-solid fa-check"></i>';
        const letter = document.createElement("span"); letter.className = "letter"; letter.textContent = String.fromCharCode(65 + i) + ".";
        const text = document.createElement("span"); text.textContent = c.content;
        label.append(input, marker, letter, text);
        input.addEventListener("change", () => {
          if (!multi) list.querySelectorAll(".choice").forEach(x => x.classList.remove("selected"));
          label.classList.toggle("selected", input.checked);
          queueSave();
        });
        list.appendChild(label);
      });
      box.appendChild(list);
    }
    el("btn-prev").disabled = q.index <= 1;
    el("btn-next").disabled = q.index >= total;
    el("report-msg").hidden = true;
    history.replaceState(null, "", `?index=${q.index}`);
    renderPalette();
  }

  // ---------- Lưu đáp án ----------
  function collectAnswer() {
    const ids = [...document.querySelectorAll("input[name='answerIds']:checked")].map(x => Number(x.value));
    const text = el("text-answer")?.value ?? null;
    return { ids, text };
  }
  async function saveAnswer() {
    if (!current) return;
    clearTimeout(saveTimer); saveTimer = null;
    const { ids, text } = collectAnswer();
    const has = ids.length > 0 || (text && text.trim().length > 0);
    if (has) answered.add(current.index); else answered.delete(current.index);
    renderPalette();
    await fetch(`${api}/answer`, {
      method: "POST", headers,
      body: JSON.stringify({ attemptId, questionId: current.questionId, answerIds: ids.length ? ids : null, textInput: text || null })
    });
  }
  function queueSave() { clearTimeout(saveTimer); saveTimer = setTimeout(saveAnswer, 250); }

  // ---------- Chuyển câu ----------
  async function go(index) {
    if (index < 1 || index > total || index === current?.index) return;
    await saveAnswer();
    const res = await fetch(`${api}/question/${attemptId}/${index}`, { headers });
    if (!res.ok) return;
    renderQuestion(await res.json());
    window.scrollTo({ top: 0 });
  }
  el("btn-prev").addEventListener("click", () => go(current.index - 1));
  el("btn-next").addEventListener("click", () => go(current.index + 1));
  document.addEventListener("keydown", e => {
    if (e.target.matches("input[type=text],input:not([type])")) return;
    if (e.key === "ArrowLeft") go(current.index - 1);
    if (e.key === "ArrowRight") go(current.index + 1);
  });

  // ---------- Đánh dấu / báo lỗi / nộp bài ----------
  el("mark-btn").addEventListener("click", () => {
    if (flagged.has(current.index)) flagged.delete(current.index); else flagged.add(current.index);
    try { localStorage.setItem(flagKey, JSON.stringify([...flagged])); } catch { /* bỏ qua */ }
    renderPalette();
  });
  el("report-btn").addEventListener("click", async () => {
    const reason = prompt("Mô tả lỗi của câu hỏi (sai đáp án, không rõ nghĩa, lỗi chính tả...):");
    if (!reason?.trim()) return;
    const res = await fetch(`${api}/report`, { method: "POST", headers, body: JSON.stringify({ attemptId, questionId: current.questionId, reason: reason.trim() }) });
    const msg = el("report-msg"); msg.hidden = false;
    msg.textContent = res.ok ? "Đã gửi báo lỗi cho giáo viên." : ((await res.json().catch(() => ({}))).message || "Không gửi được báo lỗi.");
    msg.className = "exam-msg " + (res.ok ? "ok" : "bad");
  });
  el("submit-form").addEventListener("submit", async e => {
    const left = total - answered.size;
    const ask = left > 0 ? `Bạn còn ${left} câu chưa làm. Vẫn nộp bài?` : "Nộp bài ngay bây giờ?";
    if (!confirm(ask)) { e.preventDefault(); return; }
    await saveAnswer();
  });

  // ---------- Chống gian lận cơ bản ----------
  document.addEventListener("visibilitychange", async () => {
    if (!document.hidden) return;
    tabSwitchCount++;
    warning.hidden = false;
    warning.textContent = `Cảnh báo ${tabSwitchCount}/3: rời khỏi trang thi sẽ bị ghi nhận.`;
    await fetch(`${api}/flag`, { method: "POST", headers, body: JSON.stringify({ attemptId, reason: "tab_switch", count: tabSwitchCount }) });
    if (tabSwitchCount >= 3) {
      await fetch(`${api}/submit/${attemptId}`, { method: "POST", headers });
      location.href = `/thi-sinh/ket-qua/${attemptId}`;
    }
  });
  document.addEventListener("copy", e => e.preventDefault());
  document.addEventListener("paste", e => e.preventDefault());
  document.addEventListener("contextmenu", e => e.preventDefault());

  // ---------- Khởi động ----------
  renderQuestion(JSON.parse(el("q-init").textContent));
  syncTimer();
  setInterval(() => { remainingSeconds--; renderTimer(); if (remainingSeconds <= 0) syncTimer(); }, 1000);
  setInterval(syncTimer, 30000);
})();
