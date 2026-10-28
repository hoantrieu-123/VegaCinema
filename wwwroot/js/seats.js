"use strict";
(() => {
  const root = document.getElementById("seat-picker"); if (!root) return;
  const selected = new Map(), buttons = [...root.querySelectorAll("[data-seat]")], hold = document.getElementById("hold-seats"), error = document.getElementById("seat-error");
  const currency = value => new Intl.NumberFormat("vi-VN").format(value) + " ₫";
  const render = () => {
    const list = document.getElementById("selected-seats"); list.replaceChildren();
    if (!selected.size) list.textContent = "Chưa chọn ghế";
    let total = 0;
    selected.forEach(button => { const line = document.createElement("div"), label = document.createElement("span"), price = document.createElement("strong"); label.textContent = "Ghế " + button.dataset.label; price.textContent = currency(Number(button.dataset.price)); line.append(label, price); list.append(line); total += Number(button.dataset.price); });
    document.getElementById("seat-total").textContent = currency(total); if (hold) hold.disabled = selected.size === 0;
  };
  buttons.forEach(button => button.addEventListener("click", () => {
    error.textContent = ""; const id = Number(button.dataset.seat);
    if (selected.has(id)) selected.delete(id);
    else { if (button.dataset.status !== "AVAILABLE") return; if (selected.size >= 8) { error.textContent = "Bạn có thể đặt tối đa 8 ghế mỗi đơn."; return; } selected.set(id, button); }
    button.classList.toggle("selected", selected.has(id)); button.setAttribute("aria-pressed", String(selected.has(id))); render();
  }));
  let refreshing = false;
  async function refresh() {
    if (refreshing || document.hidden) return; refreshing = true;
    try {
      const response = await fetch(`/Booking/Availability/${root.dataset.showtime}`, { headers: { Accept: "application/json" } });
      if (!response.ok) throw new Error("Không thể tải trạng thái ghế.");
      const data = await response.json();
      data.seats.forEach(seat => { const button = buttons.find(b => Number(b.dataset.seat) === seat.seatId); if (!button) return;
        button.dataset.status = seat.status; button.dataset.price = String(seat.price); button.disabled = seat.status !== "AVAILABLE";
        if (button.disabled && selected.has(seat.seatId)) { selected.delete(seat.seatId); button.classList.remove("selected"); button.setAttribute("aria-pressed", "false"); error.textContent = `Ghế ${button.dataset.label} vừa không còn khả dụng. Vui lòng chọn lại.`; }
      }); render();
    } catch (e) { error.textContent = "Chưa cập nhật được trạng thái ghế. Hệ thống sẽ thử lại."; } finally { refreshing = false; }
  }
  refresh(); const timer = setInterval(refresh, 5000); window.addEventListener("pagehide", () => clearInterval(timer));
  hold?.addEventListener("click", async () => {
    hold.disabled = true; hold.textContent = "Đang giữ ghế…"; error.textContent = "";
    try {
      const response = await fetch("/Booking/Hold", { method: "POST", headers: { "Content-Type": "application/json", Accept: "application/json", "X-CSRF-TOKEN": root.querySelector('[name="__RequestVerificationToken"]').value }, body: JSON.stringify({ showtimeId: Number(root.dataset.showtime), seatIds: [...selected.keys()] }) });
      if (response.redirected) { window.location.assign(response.url); return; }
      const body = await response.text();
      let data = null;
      if (body) {
        try { data = JSON.parse(body); } catch { throw new Error(`Máy chủ trả về phản hồi không hợp lệ (${response.status}). Vui lòng thử lại.`); }
      }
      if (!response.ok) throw new Error(data?.message || "Không thể giữ ghế. Vui lòng thử lại.");
      if (!data?.redirectUrl) throw new Error("Không nhận được đường dẫn thanh toán. Vui lòng thử lại.");
      window.location.assign(data.redirectUrl);
    } catch (e) { error.textContent = e.message; await refresh(); hold.disabled = selected.size === 0; hold.textContent = "Tiếp tục thanh toán →"; }
  });
})();
