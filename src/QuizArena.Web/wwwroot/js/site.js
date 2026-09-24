(() => {
  const toggle = document.getElementById("nav-toggle");
  const close = () => document.body.classList.remove("nav-open");
  toggle?.addEventListener("click", () => document.body.classList.toggle("nav-open"));
  document.addEventListener("keydown", e => { if (e.key === "Escape") close(); });
  document.addEventListener("click", e => { if (!e.target.closest(".site-nav")) close(); });
})();
