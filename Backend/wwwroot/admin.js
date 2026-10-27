"use strict";
const content = document.querySelector("#content"),
  filters = document.querySelector("#filters");
const titles = {
  overview: [
    "Tổng quan",
    "Sức khỏe server và hoạt động game. Thống kê hôm nay tính theo UTC.",
  ],
  players: [
    "Người chơi",
    "Tài khoản, coin và skin. Phiên hoạt động được ước tính từ heartbeat trong 90 giây gần nhất.",
  ],
  rooms: [
    "Phòng chơi",
    "Theo dõi phòng Relay, host và thành viên đã xác thực qua backend.",
  ],
  matches: [
    "Lịch sử trận",
    "Kết quả do host gửi. Trận cũ có thể chưa có danh sách người chơi hoặc thứ hạng.",
  ],
  rolls: [
    "Xúc xắc trong trận",
    "Từng lần roll, theo người chơi và trận. Dữ liệu do host gửi, tự làm mới mỗi 5 giây.",
  ],
  transactions: [
    "Giao dịch coin",
    "Thưởng và mua skin từ khi sử dụng backend. Giao dịch cũ không được suy đoán lại.",
  ],
  skins: ["Skin", "Quản lý tên, giá và trạng thái bán. Không tải ảnh preview."],
};
let view = "overview",
  page = 0,
  busy = false,
  generation = 0,
  query = "",
  matchFilter = "",
  playerFilter = "";
const esc = (v) =>
  String(v ?? "").replace(
    /[&<>"']/g,
    (c) =>
      ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[
        c
      ],
  );
const date = (v) => (v ? new Date(v).toLocaleString("vi-VN") : "—");
const num = (v) => Number(v ?? 0).toLocaleString("vi-VN");
const short = (v) => esc(v || "—");
const badge = (v, kind = "") => `<span class="badge ${kind}">${esc(v)}</span>`;
const state = (v) =>
  badge(
    v,
    ["Aborted", "Closed"].includes(v) ? "bad" : v === "Playing" ? "warn" : "",
  );
function notice(message, error = false) {
  const n = document.querySelector("#notice");
  n.hidden = !message;
  n.textContent = message;
  n.className = error ? "error" : "";
}
async function api(path, body) {
  const response = await fetch("/api/admin/" + path, {
    method: body ? "POST" : "GET",
    headers: body
      ? {
          "Content-Type": "application/json",
          "X-CSRF-TOKEN": document.querySelector("meta[name=csrf-token]")
            .content,
        }
      : {},
    body: body ? JSON.stringify(body) : undefined,
  });
  if (response.status === 401) {
    location.href = "/Login";
    throw Error("Phiên quản trị hết hạn.");
  }
  const data = await response.json();
  if (!response.ok) throw Error(data.message || "Không tải được dữ liệu.");
  return data;
}
function table(headers, rows) {
  return rows.length
    ? `<div class="table-wrap"><table><thead><tr>${headers.map((x) => `<th>${esc(x)}</th>`).join("")}</tr></thead><tbody>${rows.map((r) => `<tr>${r.map((c) => `<td>${c}</td>`).join("")}</tr>`).join("")}</tbody></table></div>`
    : '<div class="panel empty">Chưa có dữ liệu phù hợp.</div>';
}
function open(next) {
  view = next;
  page = 0;
  query = "";
  matchFilter = "";
  playerFilter = "";
  generation++;
  notice("");
  document
    .querySelectorAll("nav button")
    .forEach((b) => b.classList.toggle("active", b.dataset.view === view));
  document.querySelector("#page-title").textContent = titles[view][0];
  document.querySelector("#page-description").textContent = titles[view][1];
  filters.hidden = ["overview", "skins", "rooms"].includes(view);
  filters.innerHTML =
    view === "rolls"
      ? '<input id="match-filter" placeholder="Match ID" aria-label="Lọc theo match ID"><input id="player-filter" placeholder="Player ID" aria-label="Lọc theo player ID"><button id="search">Lọc</button>'
      : '<input id="query" placeholder="' +
        (view === "players"
          ? "Username hoặc tên hiển thị"
          : view === "matches"
            ? "Match ID, Player ID hoặc host username"
            : "Player ID hoặc mã tham chiếu") +
        '" aria-label="Tìm kiếm"><button id="search">Tìm kiếm</button>';
  content.innerHTML = '<div class="panel muted">Đang tải…</div>';
  document.querySelector("#search")?.addEventListener("click", search);
  filters.querySelectorAll("input").forEach((i) =>
    i.addEventListener("keydown", (e) => {
      if (e.key === "Enter") search();
    }),
  );
  load();
}
function search() {
  query = document.querySelector("#query")?.value.trim() || "";
  matchFilter = document.querySelector("#match-filter")?.value.trim() || "";
  playerFilter = document.querySelector("#player-filter")?.value.trim() || "";
  page = 0;
  generation++;
  load();
}
async function load() {
  if (busy) return;
  busy = true;
  const version = generation,
    currentView = view;
  try {
    const p = new URLSearchParams({
      page,
      q: query,
      matchId: matchFilter,
      playerId: playerFilter,
    });
    const data = await api(view + "?" + p);
    if (version !== generation) return;
    render(data);
    document.querySelector("#sync-status").textContent =
      "Cập nhật " + new Date().toLocaleTimeString("vi-VN");
    const size = view === "rolls" ? 50 : 30;
    document.querySelector("#pagination").hidden = data.total === undefined;
    document.querySelector("#page-number").textContent =
      `Trang ${page + 1} · ${num(data.total)} bản ghi`;
    document.querySelector("#previous").disabled = page === 0;
    document.querySelector("#next").disabled = (page + 1) * size >= data.total;
  } catch (e) {
    if (version === generation) {
      notice(e.message, true);
      document.querySelector("#sync-status").textContent = "Kết nối gián đoạn";
    }
  } finally {
    busy = false;
    if (version !== generation || currentView !== view) load();
  }
}
function render(d) {
  if (view === "overview") {
    const a = d.activity || {};
    const cards = [
      ["Người chơi hoạt động", d.onlinePlayers, "Heartbeat trong 90 giây"],
      ["Phòng đang hoạt động", d.activeRooms, "Waiting / Playing"],
      ["Trận hôm nay", d.matchesToday, "Bắt đầu từ 00:00 UTC"],
      ["Tổng tài khoản", d.players, "Tất cả người chơi"],
      ["Tài khoản mới hôm nay", d.newPlayersToday, "Đăng ký từ 00:00 UTC"],
      ["Lượt roll hôm nay", d.rollsToday, "Sự kiện đã lưu thành công"],
    ];
    content.innerHTML = `<div class="cards">${cards.map((c) => `<article class="stat"><span class="stat-label">${c[0]}</span><strong>${d.database === "Connected" ? num(c[1]) : "—"}</strong><small>${c[2]}</small></article>`).join("")}</div><div class="columns"><article class="panel"><h2>Trạng thái hệ thống</h2><div class="health-row">API ${badge("Đang chạy")}</div><div class="health-row">MongoDB ${badge(d.database, d.database === "Connected" ? "" : "bad")}</div><div class="health-row">Độ trễ MongoDB <span>${num(d.databaseLatencyMs)} ms</span></div><div class="health-row">API p95 / 500 request gần nhất <span>${num(a.p95Ms)} ms</span></div><div class="health-row">Thời gian hoạt động <span>${num(Math.floor(a.uptimeSeconds / 60))} phút</span></div><div class="health-row">Lỗi server / tổng request <span>${num(a.failures)} / ${num(a.requests)}</span></div></article><article class="panel"><h2>Lỗi gần nhất</h2>${a.errors?.length ? a.errors.map((e) => `<div class="health-row"><span>${esc(e.path)}<small class="muted"> · ${esc(e.type)}</small><p class="small muted">${date(e.time)}</p></span></div>`).join("") : '<p class="muted">Chưa ghi nhận lỗi trong phiên chạy này.</p>'}</article></div>`;
    return;
  }
  const items = d.items || [];
  if (view === "players")
    content.innerHTML = table(
      [
        "Người chơi",
        "Coin",
        "Skin đang dùng",
        "Hoạt động gần nhất",
        "Trạng thái",
        "Thao tác",
      ],
      items.map((p) => [
        `<b>${esc(p.displayName)}</b><small>${esc(p.username)}</small><small>${esc(p.id)}</small><small>Tạo: ${date(p.createdAtUtc)}</small>`,
        num(p.coins),
        `${esc(p.equippedSkinId)}<small>Sở hữu: ${esc((p.ownedSkinIds || []).join(", "))}</small>`,
        `${date(p.lastSeenUtc)}<small>Đăng nhập: ${date(p.lastLoginUtc)}</small>`,
        p.isBanned ? badge("Đã khóa", "bad") : badge("Bình thường"),
        `<button class="secondary" data-ban="${esc(p.id)}" data-value="${!p.isBanned}">${p.isBanned ? "Mở khóa" : "Khóa"}</button> <button class="secondary" data-player-rolls="${esc(p.id)}">Xúc xắc</button>`,
      ]),
    );
  if (view === "rooms")
    content.innerHTML = table(
      [
        "Phòng / host",
        "Thành viên",
        "Trạng thái",
        "Trận hiện tại",
        "Heartbeat",
      ],
      items.map((r) => [
        `<b>${esc(r.code)}</b><small>${esc(r.id)}</small><small>Host: ${esc(r.hostPlayerId)}</small>`,
        r.members.map((m) => esc(m.name)).join("<br>"),
        state(r.status) +
          " " +
          badge(r.online ? "Online" : "Offline", r.online ? "" : "bad"),
        short(r.currentMatchId),
        date(r.lastSeenUtc),
      ]),
    );
  if (view === "matches")
    content.innerHTML = table(
      [
        "Trận / host",
        "Người chơi & thứ hạng",
        "Trạng thái",
        "Thời gian",
        "Kết thúc / thưởng",
        "",
      ],
      items.map((m) => [
        `<b>${esc(m.id)}</b><small>${esc(m.hostUsername)}</small>`,
        m.participants?.length
          ? m.participants
              .map(
                (p) =>
                  `${esc(p.displayName)}${m.ranking?.includes(p.slot) ? " · #" + (m.ranking.indexOf(p.slot) + 1) : ""}`,
              )
              .join("<br>")
          : `${num(m.playerCount)} người · dữ liệu cũ`,
        state(m.status),
        `${date(m.startedAtUtc)}<small>${m.durationSeconds != null ? num(m.durationSeconds) + " giây" : "Đang chơi / chưa có thời lượng"}</small>`,
        `${esc(m.endReason || "—")}<small>Thưởng: ${num(m.rewardCoins)} coin</small>`,
        `<button class="secondary" data-match-rolls="${esc(m.id)}">Xem roll</button>`,
      ]),
    );
  if (view === "rolls")
    content.innerHTML =
      '<p class="note">Màu theo slot: Xanh dương / Đỏ / Xanh lá / Vàng. Dữ liệu chỉ có từ khi tích hợp backend; không tái tạo lịch sử cũ.</p>' +
      table(
        ["Thời gian", "Trận", "Người chơi", "Lượt", "Xúc xắc", "Tổng"],
        items.map((r) => [
          date(r.rolledAtUtc),
          esc(r.matchId),
          `<b>${esc(r.displayName)}</b><small>${["Xanh dương", "Đỏ", "Xanh lá", "Vàng"][r.slot] || "—"} · ${esc(r.playerId)}</small>`,
          num(r.sequence),
          `<div class="dice-pair"><span class="dice" title="${r.dice1}">${["", "⚀", "⚁", "⚂", "⚃", "⚄", "⚅"][r.dice1] || "?"}</span><span class="dice" title="${r.dice2}">${["", "⚀", "⚁", "⚂", "⚃", "⚄", "⚅"][r.dice2] || "?"}</span><small>${r.dice1} + ${r.dice2}</small></div>`,
          `<b>${r.total}</b>`,
        ]),
      );
  if (view === "transactions")
    content.innerHTML = table(
      [
        "Thời gian",
        "Player ID",
        "Loại",
        "Thay đổi",
        "Trước → Sau",
        "Tham chiếu",
      ],
      items.map((t) => [
        date(t.createdAtUtc),
        esc(t.playerId),
        esc(
          {
            Signup: "Tạo tài khoản",
            SkinPurchase: "Mua skin",
            MatchWin: "Thắng trận",
          }[t.kind] || t.kind,
        ),
        `<b class="${t.amount >= 0 ? "positive" : "negative"}">${t.amount > 0 ? "+" : ""}${num(t.amount)}</b>`,
        `${num(t.before)} → ${num(t.after)}`,
        esc(t.reference || t.id),
      ]),
    );
  if (view === "skins")
    content.innerHTML = table(
      ["Skin", "Mô tả", "Thiết lập"],
      items.map((s) => [
        `<b>${esc(s.name)}</b><small>${esc(s.id)}</small>`,
        esc(s.description),
        `<form class="inline-form" data-skin="${esc(s.id)}"><input type="text" name="name" aria-label="Tên skin" value="${esc(s.name)}" maxlength="80" required><input type="number" name="price" aria-label="Giá coin" value="${s.price}" min="0" max="1000000" required><label><input type="checkbox" name="sale" aria-label="Đang bán" ${s.isForSale ? "checked" : ""}>Bán</label><button type="submit">Lưu</button></form>`,
      ]),
    );
}
content.addEventListener("click", async (e) => {
  const ban = e.target.closest("[data-ban]"),
    match = e.target.closest("[data-match-rolls]"),
    player = e.target.closest("[data-player-rolls]");
  if (match || player) {
    const id = (match || player).dataset[match ? "matchRolls" : "playerRolls"];
    open("rolls");
    if (match) {
      matchFilter = id;
      document.querySelector("#match-filter").value = id;
    } else {
      playerFilter = id;
      document.querySelector("#player-filter").value = id;
    }
    generation++;
    load();
  }
  if (ban) {
    ban.disabled = true;
    try {
      const r = await api(`players/${ban.dataset.ban}/ban`, {
        isBanned: ban.dataset.value === "true",
      });
      notice(r.message);
      await load();
    } catch (error) {
      notice(error.message, true);
    } finally {
      ban.disabled = false;
    }
  }
});
content.addEventListener("submit", async (e) => {
  const form = e.target.closest("[data-skin]");
  if (!form) return;
  e.preventDefault();
  const button = form.querySelector("button");
  button.disabled = true;
  try {
    const r = await api("skins/" + encodeURIComponent(form.dataset.skin), {
      name: form.elements.name.value,
      price: Number(form.elements.price.value),
      isForSale: form.elements.sale.checked,
    });
    notice(r.message);
  } catch (error) {
    notice(error.message, true);
  } finally {
    button.disabled = false;
  }
});
document
  .querySelectorAll("nav button")
  .forEach((b) => b.addEventListener("click", () => open(b.dataset.view)));
document.querySelector("#refresh").addEventListener("click", load);
document.querySelector("#previous").addEventListener("click", () => {
  page = Math.max(0, page - 1);
  generation++;
  load();
});
document.querySelector("#next").addEventListener("click", () => {
  page++;
  generation++;
  load();
});
setInterval(() => {
  if (!document.hidden && !content.contains(document.activeElement)) load();
}, 5000);
open("overview");
