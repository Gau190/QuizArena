(() => {
  const toggle = document.querySelector(".password-toggle");
  if (!toggle) return;

  toggle.addEventListener("click", () => {
    const input = document.getElementById(toggle.dataset.target);
    const icon = toggle.querySelector("i");
    if (!input) return;

    const show = input.type === "password";
    input.type = show ? "text" : "password";
    toggle.setAttribute("aria-pressed", String(show));
    toggle.setAttribute("aria-label", show ? "Ẩn mật khẩu" : "Hiện mật khẩu");
    if (icon) {
      icon.className = show ? "fa-solid fa-eye-slash" : "fa-solid fa-eye";
    }
    input.focus();
  });
})();
