"use strict";
const content = document.querySelector("#content"),
  filters = document.querySelector("#filters");
const titles = {
  overview: [
    "Tổng quan",
    "Sức khỏe server và hoạt động game. Thống kê hôm nay tính theo UTC.",
  ],
  logs: [
    "Live Terminal Console",
    "Theo dõi log hệ thống thời gian thực từ Kestrel, TCP 7777 và Game Events.",
  ],
  players: [
    "Người chơi",
    "Tài khoản, coin và skin. Bạn có thể bấm 'Sửa' để can thiệp trực tiếp vào tài khoản.",
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
  playerFilter = "",
  currentPlayersList = [],
  logEventSource = null,
  autoScrollLogs = true;

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
  if (!n) return;
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
          "X-CSRF-TOKEN": document.querySelector("meta[name=csrf-token]")?.content || "",
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
  if (logEventSource && next !== "logs") {
    logEventSource.close();
    logEventSource = null;
  }

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

  filters.hidden = ["overview", "skins", "rooms", "logs"].includes(view);
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
    if (view === "logs") {
      renderLogs();
      document.querySelector("#sync-status").textContent = "Live Stream Connected";
      document.querySelector("#pagination").hidden = true;
      return;
    }

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
    if (data.total !== undefined) {
      document.querySelector("#page-number").textContent =
        `Trang ${page + 1} · ${num(data.total)} bản ghi`;
      document.querySelector("#previous").disabled = page === 0;
      document.querySelector("#next").disabled = (page + 1) * size >= data.total;
    }
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

function renderLogs() {
  content.innerHTML = `
    <div class="terminal-container">
      <div class="terminal-toolbar">
        <div>
          <span class="status-dot"></span> <span id="terminal-status" style="font-size: 12px; color: #50fa7b;">Streaming Live...</span>
        </div>
        <div class="actions">
          <button id="toggle-scroll" class="secondary">Auto-Scroll: BẬT</button>
          <button id="clear-logs" class="secondary">Xóa màn hình</button>
        </div>
      </div>
      <div id="terminal-window" class="terminal-window">
        <p class="muted" style="margin: 0 0 8px;">[System] Đang kết nối live stream...</p>
      </div>
    </div>
  `;

  const term = document.querySelector("#terminal-window");
  const toggleBtn = document.querySelector("#toggle-scroll");
  const clearBtn = document.querySelector("#clear-logs");

  toggleBtn.addEventListener("click", () => {
    autoScrollLogs = !autoScrollLogs;
    toggleBtn.textContent = `Auto-Scroll: ${autoScrollLogs ? "BẬT" : "TẮT"}`;
  });

  clearBtn.addEventListener("click", () => {
    term.innerHTML = '<p class="muted" style="margin: 0 0 8px;">[Terminal] Màn hình đã được xóa.</p>';
  });

  function appendLogLine(log) {
    const line = document.createElement("div");
    line.className = "terminal-log-line";
    const lvlClass = `log-lvl-${log.level || "Information"}`;
    const shortCat = (log.category || "").split(".").pop();

    line.innerHTML = `
      <span class="log-time">${esc(log.time || "")}</span>
      <span class="${lvlClass}">[${esc(log.level || "INFO")}]</span>
      <span class="log-cat">&lt;${esc(shortCat)}&gt;</span>
      <span class="log-msg">${esc(log.message || "")}</span>
    `;
    term.appendChild(line);

    if (autoScrollLogs) {
      term.scrollTop = term.scrollHeight;
    }
  }

  // 1. Load history
  api("logs/history")
    .then((res) => {
      term.innerHTML = "";
      if (res.logs && res.logs.length > 0) {
        res.logs.forEach(appendLogLine);
      } else {
        term.innerHTML = '<p class="muted">[System] Chưa có log trong phiên chạy này.</p>';
      }
    })
    .catch((err) => console.warn(err));

  // 2. Open SSE stream
  if (logEventSource) {
    logEventSource.close();
  }

  logEventSource = new EventSource("/api/admin/logs/stream");
  logEventSource.onmessage = (e) => {
    try {
      const log = JSON.parse(e.data);
      appendLogLine(log);
    } catch (err) {
      console.warn("Parse log error:", err);
    }
  };

  logEventSource.onerror = () => {
    const status = document.querySelector("#terminal-status");
    if (status) {
      status.textContent = "Reconnecting...";
      status.style.color = "#ff5555";
    }
  };

  logEventSource.onopen = () => {
    const status = document.querySelector("#terminal-status");
    if (status) {
      status.textContent = "Live Stream Active";
      status.style.color = "#50fa7b";
    }
  };
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
    content.innerHTML = `<div class="cards">${cards.map((c) => `<article class="stat"><span class="stat-label">${c[0]}</span><strong>${num(c[1])}</strong><small>${c[2]}</small></article>`).join("")}</div><div class="columns"><article class="panel"><h2>Trạng thái hệ thống</h2><div class="health-row">API ${badge("Đang chạy")}</div><div class="health-row">Database Mode ${badge(d.database, d.database === "Connected" ? "" : "warn")}</div><div class="health-row">Độ trễ Database <span>${num(d.databaseLatencyMs)} ms</span></div><div class="health-row">API p95 / 500 request gần nhất <span>${num(a.p95Ms)} ms</span></div><div class="health-row">Thời gian hoạt động <span>${num(Math.floor(a.uptimeSeconds / 60))} phút</span></div><div class="health-row">Lỗi server / tổng request <span>${num(a.failures)} / ${num(a.requests)}</span></div></article><article class="panel"><h2>Lối tắt quản trị</h2><p class="muted">Nhấn vào <b>Live Terminal</b> ở menu trái để xem trực tiếp log console của server mà không cần mở VS Code!</p><button onclick="open('logs')">Mở Live Terminal →</button></article></div>`;
    return;
  }

  const items = d.items || [];
  if (view === "players") {
    currentPlayersList = items;
    content.innerHTML = table(
      [
        "Người chơi",
        "Coin",
        "Skin đang dùng",
        "Hoạt động gần nhất",
        "Trạng thái",
        "Thao tác (GM Tool)",
      ],
      items.map((p) => [
        `<b>${esc(p.displayName || p.username)}</b><small>User: ${esc(p.username)}</small><small>ID: ${esc(p.id)}</small><small>Tạo: ${date(p.createdAtUtc)}</small>`,
        `<b style="color: #f1fa8c;">${num(p.coins)}</b>`,
        `<b>${esc(p.equippedSkinId)}</b><small>Sở hữu: ${esc((p.ownedSkinIds || []).join(", "))}</small>`,
        `${date(p.lastSeenUtc)}<small>Đăng nhập: ${date(p.lastLoginUtc)}</small>`,
        p.isBanned ? badge("Đã khóa", "bad") : badge("Bình thường"),
        `<button class="secondary" data-edit-player="${esc(p.id)}" style="background: #8be9fd; color: #101720;">✎ Sửa</button> <button class="secondary" data-ban="${esc(p.id)}" data-value="${!p.isBanned}">${p.isBanned ? "Mở khóa" : "Khóa"}</button>`,
      ]),
    );
  }

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
        (r.members || []).map((m) => esc(m.name || m.id || m)).join("<br>"),
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
          : `${num(m.playerCount)} người`,
        state(m.status),
        `${date(m.startedAtUtc)}<small>${m.durationSeconds != null ? num(m.durationSeconds) + " giây" : "Đang chơi"}</small>`,
        `${esc(m.endReason || "—")}<small>Thưởng: ${num(m.rewardCoins)} coin</small>`,
        `<button class="secondary" data-match-rolls="${esc(m.id)}">Xem roll</button>`,
      ]),
    );

  if (view === "rolls")
    content.innerHTML =
      '<p class="note">Màu theo slot: Xanh dương / Đỏ / Xanh lá / Vàng.</p>' +
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
        esc(t.kind || "Giao dịch"),
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

// Global click handlers
content.addEventListener("click", async (e) => {
  const ban = e.target.closest("[data-ban]"),
    match = e.target.closest("[data-match-rolls]"),
    player = e.target.closest("[data-player-rolls]"),
    editBtn = e.target.closest("[data-edit-player]");

  if (editBtn) {
    const id = editBtn.dataset.editPlayer;
    openEditPlayerModal(id);
    return;
  }

  if (match || player) {
    const id = (match || player).dataset[match ? "matchRolls" : "playerRolls"];
    open("rolls");
    if (match) {
      matchFilter = id;
      const el = document.querySelector("#match-filter");
      if (el) el.value = id;
    } else {
      playerFilter = id;
      const el = document.querySelector("#player-filter");
      if (el) el.value = id;
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

// Edit Player Modal Logic
async function openEditPlayerModal(playerId) {
  const player = currentPlayersList.find((p) => p.id === playerId);
  if (!player) return;

  const modal = document.querySelector("#player-modal");
  document.querySelector("#modal-player-id").value = player.id;
  document.querySelector("#modal-player-name").textContent = player.displayName || player.username;
  document.querySelector("#modal-fullname").value = player.displayName || "";
  document.querySelector("#modal-coins").value = player.coins;
  document.querySelector("#modal-banned").checked = !!player.isBanned;

  // Load available skins to populate dropdown & checkboxes
  let skins = [];
  try {
    const res = await api("skins");
    skins = res.items || [];
  } catch {
    skins = [
      { id: "default_horse", name: "Default Horse" },
      { id: "gundam_unicorn", name: "Gundam Unicorn" },
      { id: "odin_transformer", name: "Odin Transformer" },
      { id: "arya", name: "Arya" },
    ];
  }

  // Populate equipped select
  const select = document.querySelector("#modal-equipped-skin");
  select.innerHTML = skins
    .map(
      (s) =>
        `<option value="${esc(s.id)}" ${s.id === player.equippedSkinId ? "selected" : ""}>${esc(s.name)} (${esc(s.id)})</option>`,
    )
    .join("");

  // Populate skins checkboxes
  const list = document.querySelector("#modal-skins-list");
  const owned = player.ownedSkinIds || ["default_horse"];
  list.innerHTML = skins
    .map(
      (s) => `
    <label>
      <input type="checkbox" name="owned_skin" value="${esc(s.id)}" ${owned.includes(s.id) ? "checked" : ""}>
      <span>${esc(s.name)}</span>
    </label>
  `,
    )
    .join("");

  modal.hidden = false;
}

function closeEditPlayerModal() {
  const modal = document.querySelector("#player-modal");
  if (modal) modal.hidden = true;
}

document.querySelector("#modal-close")?.addEventListener("click", closeEditPlayerModal);
document.querySelector("#modal-cancel")?.addEventListener("click", closeEditPlayerModal);

document.querySelector("#edit-player-form")?.addEventListener("submit", async (e) => {
  e.preventDefault();
  const id = document.querySelector("#modal-player-id").value;
  const fullName = document.querySelector("#modal-fullname").value;
  const coins = Number(document.querySelector("#modal-coins").value);
  const equippedSkinId = document.querySelector("#modal-equipped-skin").value;
  const isBanned = document.querySelector("#modal-banned").checked;

  const ownedSkinIds = Array.from(
    document.querySelectorAll('input[name="owned_skin"]:checked'),
  ).map((cb) => cb.value);

  if (!ownedSkinIds.includes("default_horse")) {
    ownedSkinIds.unshift("default_horse");
  }

  try {
    const res = await api(`players/${id}/edit`, {
      fullName,
      coins,
      equippedSkinId,
      ownedSkinIds,
      isBanned,
    });
    notice(res.message || "Đã lưu thay đổi!");
    closeEditPlayerModal();
    await load();
  } catch (err) {
    notice(err.message, true);
  }
});

// Skin edit form handler
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

document.querySelector("#refresh")?.addEventListener("click", load);
document.querySelector("#previous")?.addEventListener("click", () => {
  page = Math.max(0, page - 1);
  generation++;
  load();
});
document.querySelector("#next")?.addEventListener("click", () => {
  page++;
  generation++;
  load();
});

setInterval(() => {
  if (view !== "logs" && !document.hidden && !content.contains(document.activeElement)) {
    load();
  }
}, 5000);

open("overview");
