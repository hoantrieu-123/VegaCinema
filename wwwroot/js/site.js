"use strict";
document.querySelector(".menu-toggle")?.addEventListener("click", function () {
  const open = this.getAttribute("aria-expanded") !== "true";
  this.setAttribute("aria-expanded", String(open)); document.querySelector(".main-nav")?.classList.toggle("open", open);
});
document.querySelectorAll("[data-confirm]").forEach(form => form.addEventListener("submit", event => { if (!window.confirm(form.dataset.confirm)) event.preventDefault(); }));
document.querySelectorAll("[data-print]").forEach(button => button.addEventListener("click", () => window.print()));
const countdown = document.querySelector("[data-expires]");
if (countdown) {
  const expiry = Date.parse(countdown.dataset.expires), serverNow = Date.parse(countdown.dataset.serverNow), started = performance.now();
  const tick = () => {
    const remaining = Math.max(0, Math.ceil((expiry - serverNow - (performance.now() - started)) / 1000));
    document.getElementById("countdown").textContent = `${String(Math.floor(remaining / 60)).padStart(2, "0")}:${String(remaining % 60).padStart(2, "0")}`;
    if (!remaining) { const button = document.getElementById("pay-button"); if (button) button.disabled = true; const link = document.getElementById("demo-payment-link"); if (link) { link.setAttribute("aria-disabled", "true"); link.tabIndex = -1; } countdown.classList.add("expired"); const status = document.getElementById("payment-status"); if (status) status.textContent = "Thời gian giữ ghế đã hết. Vui lòng đặt lại."; }
  };
  tick(); setInterval(tick, 500);
}
document.getElementById("payment-form")?.addEventListener("submit", () => {
  const button = document.getElementById("pay-button"); button.disabled = true; button.textContent = "Đang xác nhận giao dịch…";
  document.getElementById("payment-status").textContent = "Đang xử lý thanh toán. Vui lòng chờ.";
});
