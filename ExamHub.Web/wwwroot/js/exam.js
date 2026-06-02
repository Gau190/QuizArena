(() => {
  const root = document.querySelector(".exam-shell");
  if (!root) return;

  const attemptId = root.dataset.attemptId;
  const questionId = Number(root.dataset.questionId);
  const timer = document.getElementById("timer-display");
  const warning = document.getElementById("anti-cheat");
  let remainingSeconds = 0;
  let tabSwitchCount = 0;
  let saveTimer = null;

  const token = document.cookie
    .split("; ")
    .find(x => x.startsWith("ExamHubToken="))
    ?.split("=")[1];

  const headers = { "Content-Type": "application/json" };
  if (token) headers.Authorization = "Bearer " + decodeURIComponent(token);

  function renderTimer() {
    const safe = Math.max(0, remainingSeconds);
    const mins = Math.floor(safe / 60);
    const secs = safe % 60;
    timer.textContent = `${String(mins).padStart(2, "0")}:${String(secs).padStart(2, "0")}`;
    timer.classList.toggle("low", safe <= 300);
  }

  async function syncTimer() {
    const res = await fetch(`/api/exam/time-remaining/${attemptId}`, { headers });
    if (!res.ok) return;
    const data = await res.json();
    remainingSeconds = data.remaining || 0;
    renderTimer();
    if (data.autoSubmitted) location.href = `/student/result/${attemptId}`;
  }

  async function saveAnswer() {
    const checked = [...document.querySelectorAll("input[name='answerIds']:checked")].map(x => Number(x.value));
    const textInput = document.getElementById("text-answer")?.value || null;
    await fetch("/api/exam/answer", {
      method: "POST",
      headers,
      body: JSON.stringify({ attemptId, questionId, answerIds: checked.length ? checked : null, textInput })
    });
  }

  function queueSave() {
    clearTimeout(saveTimer);
    saveTimer = setTimeout(saveAnswer, 250);
  }

  document.querySelectorAll("input[name='answerIds'], #text-answer").forEach(el => {
    el.addEventListener("change", queueSave);
    el.addEventListener("input", queueSave);
  });

  document.addEventListener("visibilitychange", async () => {
    if (!document.hidden) return;
    tabSwitchCount++;
    warning.hidden = false;
    warning.textContent = `Cảnh báo ${tabSwitchCount}/3: Chuyển tab sẽ bị ghi nhận vi phạm!`;
    await fetch("/api/exam/flag", {
      method: "POST",
      headers,
      body: JSON.stringify({ attemptId, reason: "tab_switch", count: tabSwitchCount })
    });
    if (tabSwitchCount >= 3) {
      await fetch(`/api/exam/submit/${attemptId}`, { method: "POST", headers });
      location.href = `/student/result/${attemptId}`;
    }
  });

  document.addEventListener("copy", e => e.preventDefault());
  document.addEventListener("paste", e => e.preventDefault());
  document.addEventListener("contextmenu", e => e.preventDefault());
  document.addEventListener("keydown", e => {
    if (e.key === "F12" || (e.ctrlKey && e.shiftKey && e.key.toLowerCase() === "i")) e.preventDefault();
  });

  document.querySelectorAll(".exam-actions a, .question-grid a").forEach(a => {
    a.addEventListener("click", async e => {
      e.preventDefault();
      await saveAnswer();
      location.href = a.href;
    });
  });

  syncTimer();
  setInterval(() => {
    remainingSeconds--;
    renderTimer();
    if (remainingSeconds <= 0) syncTimer();
  }, 1000);
  setInterval(syncTimer, 30000);
})();
