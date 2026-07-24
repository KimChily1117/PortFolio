const API_BASE_URL = "http://127.0.0.1:8090";
const REFRESH_INTERVAL_MS = 3000;
const DEFAULT_LAUNCHER_CONFIG = {
  gameName: "Project Dawn",
  gameExecutablePath: "E:\\task\\C#\\Project_Dawn\\Builds\\Win64\\Project_Dawn1\\Project_Dawn1.exe",
  gameStartCommand: "start \"\" /D \"E:\\task\\C#\\Project_Dawn\\Builds\\Win64\\Project_Dawn1\" \"E:\\task\\C#\\Project_Dawn\\Builds\\Win64\\Project_Dawn1\\Project_Dawn1.exe\" -screen-width 800 -screen-height 600 -screen-fullscreen 0 -testClientId=build01",
  gameClients: [],
  customProtocolUrl: "projectdawn://launch",
  customProtocolEnabled: false
};
const launcherConfig = Object.assign({}, DEFAULT_LAUNCHER_CONFIG, window.ProjectDawnLauncherConfig || {});

let autoRefreshTimer = null;
let refreshInFlight = false;
let lastRooms = [];

const els = {
  onlineBadge: document.getElementById("onlineBadge"),
  apiBaseText: document.getElementById("apiBaseText"),
  lastUpdated: document.getElementById("lastUpdated"),
  refreshButton: document.getElementById("refreshButton"),
  autoRefreshToggle: document.getElementById("autoRefreshToggle"),
  errorPanel: document.getElementById("errorPanel"),
  roomsGrid: document.getElementById("roomsGrid"),
  roomCountText: document.getElementById("roomCountText"),
  townChannelsGrid: document.getElementById("townChannelsGrid"),
  townChannelCountText: document.getElementById("townChannelCountText"),
  statusOnline: document.getElementById("statusOnline"),
  totalRooms: document.getElementById("totalRooms"),
  townRooms: document.getElementById("townRooms"),
  dungeonRooms: document.getElementById("dungeonRooms"),
  totalPlayers: document.getElementById("totalPlayers"),
  totalEnemies: document.getElementById("totalEnemies"),
  onlinePlayers: document.getElementById("onlinePlayers"),
  matchingWaitingPlayers: document.getElementById("matchingWaitingPlayers"),
  matchingQueueCount: document.getElementById("matchingQueueCount"),
  maxRoomUpdateMs: document.getElementById("maxRoomUpdateMs"),
  snapshotUpdatedAtUtc: document.getElementById("snapshotUpdatedAtUtc"),
  onlinePlayersCount: document.getElementById("onlinePlayersCount"),
  onlinePlayersStatus: document.getElementById("onlinePlayersStatus"),
  onlinePlayersList: document.getElementById("onlinePlayersList"),
  matchingWaitingCount: document.getElementById("matchingWaitingCount"),
  matchingQueueCountText: document.getElementById("matchingQueueCountText"),
  matchingQueueStatus: document.getElementById("matchingQueueStatus"),
  matchingQueuesList: document.getElementById("matchingQueuesList"),
  recentEventsCount: document.getElementById("recentEventsCount"),
  recentEventsStatus: document.getElementById("recentEventsStatus"),
  recentEventsList: document.getElementById("recentEventsList"),
  rejectReasonsCount: document.getElementById("rejectReasonsCount"),
  rejectReasonsStatus: document.getElementById("rejectReasonsStatus"),
  rejectReasonsList: document.getElementById("rejectReasonsList"),
  gameName: document.getElementById("gameName"),
  gameExecutablePath: document.getElementById("gameExecutablePath"),
  launchCommandText: document.getElementById("launchCommandText"),
  launchModeText: document.getElementById("launchModeText"),
  launchStatusBadge: document.getElementById("launchStatusBadge"),
  launchMessage: document.getElementById("launchMessage"),
  startGameButton: document.getElementById("startGameButton"),
  startAllClientsButton: document.getElementById("startAllClientsButton"),
  clientLaunchButtons: document.getElementById("clientLaunchButtons"),
  copyLaunchCommandButton: document.getElementById("copyLaunchCommandButton"),
  openSetupGuideButton: document.getElementById("openSetupGuideButton"),
  customProtocolToggle: document.getElementById("customProtocolToggle")
};

function text(value, fallback = "-") {
  if (value === null || value === undefined || value === "") return fallback;
  return String(value);
}

function num(value, digits = 0) {
  if (typeof value !== "number" || Number.isNaN(value)) return "-";
  return digits > 0 ? value.toFixed(digits) : String(Math.round(value));
}

function formatDate(value) {
  if (!value) return "-";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return text(value);
  return date.toLocaleString();
}

function setText(idOrElement, value) {
  const el = typeof idOrElement === "string" ? document.getElementById(idOrElement) : idOrElement;
  if (el) el.textContent = value;
}

function el(tag, className, value) {
  const node = document.createElement(tag);
  if (className) node.className = className;
  if (value !== undefined) node.textContent = value;
  return node;
}

async function fetchJson(path) {
  const response = await fetch(`${API_BASE_URL}${path}`, { cache: "no-store" });
  if (!response.ok) {
    let detail = response.statusText;
    try {
      const body = await response.json();
      detail = body.error || JSON.stringify(body);
    } catch (_) {}
    throw new Error(`${path} failed: ${response.status} ${detail}`);
  }
  return response.json();
}

async function refreshAll() {
  if (refreshInFlight) return;
  refreshInFlight = true;
  try {
    clearError();
    await fetchJson("/api/health");
    const results = await Promise.allSettled([
      fetchJson("/api/server/status"),
      fetchJson("/api/rooms")
    ]);

    setOnlineState(true);
    if (results[0].status === "fulfilled") renderStatus(results[0].value);
    else renderError(results[0].reason);

    if (results[1].status === "fulfilled") {
      lastRooms = Array.isArray(results[1].value) ? results[1].value : [];
      renderRooms(lastRooms);
    } else {
      renderError(results[1].reason);
      renderRooms(lastRooms);
    }

    await refreshOptionalMonitoring();
    setText(els.lastUpdated, new Date().toLocaleTimeString());
  } catch (error) {
    setOnlineState(false);
    renderError(error);
    renderRooms(lastRooms);
    renderOnlinePlayers(null, error);
    renderMatchingQueues(null, error);
    renderRecentEvents(null, error);
    renderRejectReasons(null, error);
    setText(els.lastUpdated, new Date().toLocaleTimeString());
  } finally {
    refreshInFlight = false;
  }
}

async function refreshOptionalMonitoring() {
  const results = await Promise.allSettled([
    fetchJson("/api/players/online"),
    fetchJson("/api/matching/queue"),
    fetchJson("/api/events/recent")
  ]);
  renderOnlinePlayers(results[0].status === "fulfilled" ? results[0].value : null, results[0].status === "rejected" ? results[0].reason : null);
  renderMatchingQueues(results[1].status === "fulfilled" ? results[1].value : null, results[1].status === "rejected" ? results[1].reason : null);
  const eventsSnapshot = results[2].status === "fulfilled" ? results[2].value : null;
  const eventsError = results[2].status === "rejected" ? results[2].reason : null;
  renderRecentEvents(eventsSnapshot, eventsError);
  renderRejectReasons(eventsSnapshot, eventsError);
}

function setOnlineState(isOnline) {
  els.onlineBadge.textContent = isOnline ? "Online" : "Offline";
  els.onlineBadge.classList.toggle("badge-online", isOnline);
  els.onlineBadge.classList.toggle("badge-offline", !isOnline);
  setText(els.statusOnline, isOnline ? "True" : "False");
}

function renderStatus(status) {
  setText(els.totalRooms, text(status && status.totalRooms));
  setText(els.townRooms, text(status && status.townRooms));
  setText(els.dungeonRooms, text(status && status.dungeonRooms));
  setText(els.totalPlayers, text(status && status.totalPlayers));
  setText(els.totalEnemies, text(status && status.totalEnemies));
  setText(els.onlinePlayers, text(status && status.onlinePlayers));
  setText(els.matchingWaitingPlayers, text(status && status.matchingWaitingPlayers));
  setText(els.matchingQueueCount, text(status && status.matchingQueueCount));
  setText(els.maxRoomUpdateMs, `${num(status && status.maxRoomUpdateMs, 2)} ms`);
  setText(els.snapshotUpdatedAtUtc, formatDate(status && status.snapshotUpdatedAtUtc));
}

function renderError(error) {
  els.errorPanel.classList.remove("hidden");
  els.errorPanel.textContent = `Monitoring API unavailable: ${error && error.message ? error.message : error}`;
}

function clearError() {
  els.errorPanel.classList.add("hidden");
  els.errorPanel.textContent = "";
}

function renderRooms(rooms) {
  els.roomsGrid.replaceChildren();
  const safeRooms = Array.isArray(rooms) ? rooms : [];
  renderTownChannels(safeRooms);
  setText(els.roomCountText, `${safeRooms.length} room${safeRooms.length === 1 ? "" : "s"}`);

  if (safeRooms.length === 0) {
    els.roomsGrid.appendChild(el("div", "empty", "No room snapshots available."));
    return;
  }

  safeRooms.forEach(room => els.roomsGrid.appendChild(renderRoomCard(room || {})));
}

function renderTownChannels(rooms) {
  if (!els.townChannelsGrid) return;
  els.townChannelsGrid.replaceChildren();
  const townRooms = (Array.isArray(rooms) ? rooms : [])
    .filter(room => String(room && room.roomType || "").toLowerCase() === "town")
    .sort((a, b) => Number(a.roomId || 0) - Number(b.roomId || 0));

  setText(els.townChannelCountText, `${townRooms.length} channel${townRooms.length === 1 ? "" : "s"}`);

  if (townRooms.length === 0) {
    els.townChannelsGrid.appendChild(el("div", "empty", "No town channels online."));
    return;
  }

  townRooms.forEach((room, index) => els.townChannelsGrid.appendChild(renderTownChannelCard(room || {}, index + 1)));
}

function renderTownChannelCard(room, channelIndex) {
  const maxPlayers = 50;
  const playerCount = Number(room.playerCount || 0);
  const fill = maxPlayers > 0 ? Math.max(0, Math.min(100, (playerCount / maxPlayers) * 100)) : 0;
  const stateClass = playerCount >= maxPlayers ? "full" : playerCount >= Math.floor(maxPlayers * 0.8) ? "busy" : "open";
  const stateText = playerCount >= maxPlayers ? "Full" : playerCount >= Math.floor(maxPlayers * 0.8) ? "Busy" : "Open";

  const card = el("article", `channel-card ${stateClass}`);
  const head = el("div", "channel-head");
  const title = el("div");
  title.appendChild(el("h3", null, `Channel ${channelIndex}`));
  title.appendChild(el("span", "entity-muted", `Room ${text(room.roomId)}`));
  head.appendChild(title);
  head.appendChild(statusBadge(stateText, stateClass === "full" ? "badge-warning" : stateClass === "busy" ? "badge-info" : "badge-ready"));
  card.appendChild(head);

  const stats = el("div", "channel-stats");
  stats.appendChild(statPill("Players", `${playerCount}/${maxPlayers}`));
  stats.appendChild(statPill("Last ms", num(room.lastUpdateMs, 2)));
  stats.appendChild(statPill("Max ms", num(room.maxUpdateMs, 2)));
  card.appendChild(stats);

  const bar = el("div", "channel-bar");
  const fillEl = el("div", "channel-fill");
  fillEl.style.width = `${fill}%`;
  bar.appendChild(fillEl);
  card.appendChild(bar);
  return card;
}
function renderRoomCard(room) {
  const type = text(room.roomType, "Unknown");
  const card = el("article", `room-card ${type.toLowerCase()}`);
  const head = el("div", "room-head");
  const titleWrap = el("div");
  titleWrap.appendChild(el("h3", null, `Room ${text(room.roomId)}`));
  titleWrap.appendChild(el("div", "room-type", type));
  head.appendChild(titleWrap);
  head.appendChild(el("div", "room-state", `State: ${text(room.state)}`));
  card.appendChild(head);

  const stats = el("div", "room-stats");
  stats.appendChild(statPill("Players", text(room.playerCount)));
  stats.appendChild(statPill("Enemies", text(room.enemyCount)));
  stats.appendChild(statPill("Last ms", num(room.lastUpdateMs, 2)));
  stats.appendChild(statPill("Max ms", num(room.maxUpdateMs, 2)));
  card.appendChild(stats);

  if (type.toLowerCase() === "bakal" || room.bossName) card.appendChild(renderBoss(room));
  card.appendChild(renderEntitySection("Players", room.players || [], renderPlayerRow));
  card.appendChild(renderEntitySection("Enemies", room.enemies || [], renderEnemyRow));
  return card;
}

function statPill(label, value) {
  const pill = el("div", "stat-pill");
  pill.appendChild(el("span", null, label));
  pill.appendChild(el("strong", null, value));
  return pill;
}

function renderBoss(room) {
  const boss = el("div", "boss-box");
  const row = el("div", "boss-row");
  row.appendChild(el("strong", null, text(room.bossName, "No boss")));
  row.appendChild(el("span", "entity-muted", `${text(room.bossHp, "0")}/${text(room.bossMaxHp, "0")}${room.bossIsDead ? " Dead" : ""}`));
  boss.appendChild(row);
  const max = Number(room.bossMaxHp || 0);
  const hp = Math.max(0, Number(room.bossHp || 0));
  const percent = max > 0 ? Math.max(0, Math.min(100, (hp / max) * 100)) : 0;
  const bar = el("div", "hp-bar");
  const fill = el("div", "hp-fill");
  fill.style.width = `${percent}%`;
  bar.appendChild(fill);
  boss.appendChild(bar);
  return boss;
}

function renderEntitySection(title, items, rowRenderer) {
  const section = el("section", "entity-section");
  section.appendChild(el("div", "entity-title", title));
  const list = el("div", "entity-list");
  if (!Array.isArray(items) || items.length === 0) {
    list.appendChild(el("div", "empty", `No ${title.toLowerCase()}.`));
  } else {
    items.forEach(item => list.appendChild(rowRenderer(item || {})));
  }
  section.appendChild(list);
  return section;
}

function renderPlayerRow(player) {
  const row = el("div", "entity-row");
  row.appendChild(el("span", "entity-name", text(player.name, "Unknown")));
  row.appendChild(el("span", "entity-muted", `#${text(player.objectId)}`));
  row.appendChild(el("span", "entity-muted", `HP ${text(player.hp)}/${text(player.maxHp)}`));
  row.appendChild(el("span", "entity-muted", `(${num(player.posX, 2)}, ${num(player.posY, 2)})`));
  row.appendChild(el("span", "entity-muted", text(player.state)));
  return row;
}

function renderEnemyRow(enemy) {
  const row = el("div", "entity-row");
  row.appendChild(el("span", "entity-name", text(enemy.name, "Unknown")));
  row.appendChild(el("span", "entity-muted", `#${text(enemy.objectId)}`));
  row.appendChild(el("span", "entity-muted", `HP ${text(enemy.hp)}/${text(enemy.maxHp)}`));
  row.appendChild(el("span", "entity-muted", `(${num(enemy.posX, 2)}, ${num(enemy.posY, 2)})`));
  row.appendChild(el("span", "entity-muted", enemy.isDead ? "Dead" : "Alive"));
  return row;
}

function renderOnlinePlayers(snapshot, error) {
  if (!els.onlinePlayersList) return;
  els.onlinePlayersList.replaceChildren();
  if (error) {
    setText(els.onlinePlayersCount, "Unavailable");
    setText(els.onlinePlayersStatus, "Unavailable");
    els.onlinePlayersList.appendChild(el("div", "empty", "Online players endpoint unavailable."));
    return;
  }
  const players = snapshot && Array.isArray(snapshot.players) ? snapshot.players : [];
  setText(els.onlinePlayersCount, `${text(snapshot && snapshot.count, players.length)} player${players.length === 1 ? "" : "s"}`);
  setText(els.onlinePlayersStatus, "Ready");
  if (players.length === 0) {
    els.onlinePlayersList.appendChild(el("div", "empty", "No online players."));
    return;
  }
  players.forEach(player => els.onlinePlayersList.appendChild(renderOnlinePlayerRow(player || {})));
}

function renderOnlinePlayerRow(player) {
  const row = el("div", "monitor-row player-monitor-row");
  const main = el("div", "monitor-main");
  main.appendChild(el("strong", null, text(player.name, "Unknown")));
  main.appendChild(el("span", "entity-muted", `Session ${text(player.sessionId)} / Object ${text(player.objectId)}`));
  const badges = el("div", "badge-row");
  badges.appendChild(statusBadge(text(player.roomType, "No Room"), roomBadgeClass(player.roomType)));
  if (player.roomId !== null && player.roomId !== undefined) badges.appendChild(statusBadge(`Room ${player.roomId}`, "badge-info"));
  if (player.isTransferring) badges.appendChild(statusBadge("Transferring", "badge-warning"));
  const meta = el("div", "monitor-meta");
  meta.appendChild(el("span", null, `HP ${text(player.hp)}/${text(player.maxHp)}`));
  meta.appendChild(el("span", null, `Pos (${num(player.posX, 2)}, ${num(player.posY, 2)})`));
  meta.appendChild(el("span", null, text(player.state)));
  row.appendChild(main);
  row.appendChild(badges);
  row.appendChild(meta);
  return row;
}

function renderMatchingQueues(snapshot, error) {
  if (!els.matchingQueuesList) return;
  els.matchingQueuesList.replaceChildren();
  if (error) {
    setText(els.matchingWaitingCount, "Unavailable");
    setText(els.matchingQueueCountText, "Unavailable");
    setText(els.matchingQueueStatus, "Unavailable");
    els.matchingQueuesList.appendChild(el("div", "empty", "Matching queue endpoint unavailable."));
    return;
  }
  const queues = snapshot && Array.isArray(snapshot.queues) ? snapshot.queues : [];
  const waiting = snapshot && typeof snapshot.totalWaitingPlayers === "number" ? snapshot.totalWaitingPlayers : 0;
  const queueCount = snapshot && typeof snapshot.queueCount === "number" ? snapshot.queueCount : queues.length;
  setText(els.matchingWaitingCount, `${waiting} waiting`);
  setText(els.matchingQueueCountText, `${queueCount} queue${queueCount === 1 ? "" : "s"}`);
  setText(els.matchingQueueStatus, "Ready");
  if (queues.length === 0) {
    els.matchingQueuesList.appendChild(el("div", "empty", "No waiting matching queues."));
    return;
  }
  queues.forEach(queue => els.matchingQueuesList.appendChild(renderMatchingQueue(queue || {})));
}

function renderMatchingQueue(queue) {
  const card = el("div", "queue-card");
  const head = el("div", "queue-head");
  const title = el("div");
  title.appendChild(el("strong", null, text(queue.queueKey, "Unknown Queue")));
  title.appendChild(el("span", "entity-muted", `${text(queue.waitingCount, 0)}/${text(queue.partySize, "-")} waiting`));
  head.appendChild(title);
  head.appendChild(statusBadge(text(queue.roomType || queue.dungeonType, "Waiting"), roomBadgeClass(queue.roomType || queue.dungeonType)));
  card.appendChild(head);
  const players = Array.isArray(queue.players) ? queue.players : [];
  if (players.length === 0) {
    card.appendChild(el("div", "empty", "No waiting players."));
    return card;
  }
  const list = el("div", "queue-player-list");
  players.forEach(player => {
    const row = el("div", "queue-player-row");
    row.appendChild(el("span", "entity-name", text(player.playerName, "Unknown")));
    row.appendChild(el("span", "entity-muted", `Lv ${text(player.level, 0)} / MMR ${text(player.mmr, 0)}`));
    row.appendChild(statusBadge(player.hasWeapon && player.hasArmor ? "Ready" : "Missing Gear", player.hasWeapon && player.hasArmor ? "badge-ready" : "badge-warning"));
    row.appendChild(el("span", "entity-muted", `${num(player.waitingSeconds, 0)}s`));
    list.appendChild(row);
  });
  card.appendChild(list);
  return card;
}

function renderRecentEvents(snapshot, error) {
  if (!els.recentEventsList) return;
  els.recentEventsList.replaceChildren();
  if (error) {
    setText(els.recentEventsCount, "Unavailable");
    setText(els.recentEventsStatus, "Unavailable");
    els.recentEventsList.appendChild(el("div", "empty", "Recent events endpoint unavailable."));
    return;
  }
  const events = snapshot && Array.isArray(snapshot.events) ? snapshot.events : [];
  const count = snapshot && typeof snapshot.count === "number" ? snapshot.count : events.length;
  const max = snapshot && typeof snapshot.maxEvents === "number" ? snapshot.maxEvents : "-";
  setText(els.recentEventsCount, `${count}/${max} events`);
  setText(els.recentEventsStatus, "Ready");
  if (events.length === 0) {
    els.recentEventsList.appendChild(el("div", "empty", "No recent events."));
    return;
  }
  events.slice(0, 12).forEach(event => els.recentEventsList.appendChild(renderRecentEventRow(event || {})));
}

function renderRecentEventRow(event) {
  const row = el("div", "monitor-row event-monitor-row");
  const main = el("div", "monitor-main");
  main.appendChild(el("strong", null, text(event.type, "Unknown")));
  main.appendChild(el("span", "entity-muted", formatDate(event.occurredAtUtc)));
  const badges = el("div", "badge-row");
  if (event.reason) badges.appendChild(statusBadge(event.reason, "badge-warning"));
  if (event.roomId !== null && event.roomId !== undefined) badges.appendChild(statusBadge(`Room ${event.roomId}`, roomBadgeClass(event.roomType)));
  if (event.partyId !== null && event.partyId !== undefined) badges.appendChild(statusBadge(`Party ${event.partyId}`, "badge-info"));
  const meta = el("div", "monitor-meta");
  if (event.playerName) meta.appendChild(el("span", null, event.playerName));
  if (event.queueKey) meta.appendChild(el("span", null, event.queueKey));
  if (event.transferId !== null && event.transferId !== undefined) meta.appendChild(el("span", null, `Transfer ${event.transferId}`));
  if (event.detail) meta.appendChild(el("span", null, event.detail));
  row.appendChild(main);
  row.appendChild(badges);
  row.appendChild(meta);
  return row;
}

function renderRejectReasons(snapshot, error) {
  if (!els.rejectReasonsList) return;
  els.rejectReasonsList.replaceChildren();
  if (error) {
    setText(els.rejectReasonsCount, "Unavailable");
    setText(els.rejectReasonsStatus, "Unavailable");
    els.rejectReasonsList.appendChild(el("div", "empty", "Reject reason counters unavailable."));
    return;
  }
  const counts = snapshot && snapshot.rejectReasonCounts ? snapshot.rejectReasonCounts : {};
  const entries = Object.entries(counts).sort((a, b) => b[1] - a[1] || a[0].localeCompare(b[0]));
  const total = entries.reduce((sum, pair) => sum + Number(pair[1] || 0), 0);
  setText(els.rejectReasonsCount, `${total} reject${total === 1 ? "" : "s"}`);
  setText(els.rejectReasonsStatus, "Ready");
  if (entries.length === 0) {
    els.rejectReasonsList.appendChild(el("div", "empty", "No combat rejects recorded."));
    return;
  }
  entries.forEach(([reason, count]) => {
    const row = el("div", "queue-player-row reject-reason-row");
    row.appendChild(el("span", "entity-name", reason));
    row.appendChild(el("span", "entity-muted", `${count}`));
    row.appendChild(statusBadge("Combat", "badge-warning"));
    row.appendChild(el("span", "entity-muted", "counter"));
    els.rejectReasonsList.appendChild(row);
  });
}
function statusBadge(value, className) {
  return el("span", `mini-badge ${className || "badge-info"}`, value);
}

function roomBadgeClass(roomType) {
  const value = String(roomType || "").toLowerCase();
  if (value === "town") return "badge-town";
  if (value === "bakal") return "badge-bakal";
  return "badge-info";
}

function setLaunchStatus(status, message, tone = "info") {
  setText(els.launchStatusBadge, status);
  setText(els.launchMessage, message);
  if (els.launchMessage) els.launchMessage.className = `launcher-message ${tone}`;
}

function renderLauncherConfig() {
  setText(els.gameName, launcherConfig.gameName || "Project Dawn");
  setText(els.gameExecutablePath, launcherConfig.gameExecutablePath || "Not configured");
  setText(els.launchCommandText, launcherConfig.gameStartCommand || "Not configured");
  if (els.customProtocolToggle) els.customProtocolToggle.checked = Boolean(launcherConfig.customProtocolEnabled);
  renderLaunchMode();
  setLaunchStatus("Ready", "Ready. If the Unity build does not exist yet, create a Windows build or update launcher-config.js.", "info");
}

function renderLaunchMode() {
  setText(els.launchModeText, launcherConfig.customProtocolEnabled ? "Custom protocol mode" : "Copy command mode");
}

function getConfiguredClients() {
  if (Array.isArray(launcherConfig.gameClients) && launcherConfig.gameClients.length > 0) return launcherConfig.gameClients;
  return [{
    index: 1,
    label: "Client 1",
    executablePath: launcherConfig.gameExecutablePath,
    startCommand: launcherConfig.gameStartCommand,
    protocolUrl: launcherConfig.customProtocolUrl
  }];
}

function renderClientLaunchButtons() {
  if (!els.clientLaunchButtons) return;
  els.clientLaunchButtons.replaceChildren();
  getConfiguredClients().forEach(client => {
    const button = document.createElement("button");
    button.type = "button";
    button.textContent = client.label || `Client ${client.index || ""}`.trim();
    button.addEventListener("click", () => startClient(client));
    els.clientLaunchButtons.appendChild(button);
  });
}

async function copyLaunchCommand(commandOverride) {
  const command = commandOverride || launcherConfig.gameStartCommand || "";
  if (!command) {
    setLaunchStatus("Browser cannot directly launch local executable", "Launch command is not configured. Update launcher-config.js first.", "warning");
    return false;
  }
  try {
    if (navigator.clipboard && window.isSecureContext) await navigator.clipboard.writeText(command);
    else copyTextFallback(command);
    setLaunchStatus("Command copied", "Launch command copied. Paste it into PowerShell or the Windows Run dialog.", "success");
    return true;
  } catch (_) {
    copyTextFallback(command);
    setLaunchStatus("Command copied", "Launch command copied. If paste is unavailable, copy it from the Launch Command field.", "success");
    return true;
  }
}

function copyTextFallback(value) {
  const textarea = document.createElement("textarea");
  textarea.value = value;
  textarea.setAttribute("readonly", "");
  textarea.style.position = "fixed";
  textarea.style.left = "-9999px";
  document.body.appendChild(textarea);
  textarea.select();
  document.execCommand("copy");
  textarea.remove();
}

async function startGame() {
  const firstClient = getConfiguredClients()[0];
  if (firstClient) {
    await startClient(firstClient);
    return;
  }
  if (launcherConfig.customProtocolEnabled) {
    setLaunchStatus("Custom protocol launch requested", "Custom protocol launch requested. The browser may show a security confirmation prompt.", "info");
    window.location.href = launcherConfig.customProtocolUrl || "projectdawn://launch";
    return;
  }
  setLaunchStatus("Browser cannot directly launch local executable", "Custom protocol is disabled. Browsers cannot directly launch local executable files, so the launch command will be copied instead.", "warning");
  await copyLaunchCommand();
  setLaunchStatus("Browser cannot directly launch local executable", "Launch command copied. Paste it into PowerShell or the Windows Run dialog.", "warning");
}

async function startAllClients() {
  const clients = getConfiguredClients();
  if (launcherConfig.customProtocolEnabled) {
    const protocolUrl = launcherConfig.customProtocolAllUrl || "projectdawn://launch?client=all";
    setLaunchStatus("All clients launch requested", "Client 1~4 launch requested. The browser may show a security confirmation prompt.", "info");
    window.location.href = protocolUrl;
    return;
  }

  const commands = clients
    .map(client => client && client.startCommand)
    .filter(command => command && command.trim().length > 0)
    .join("\r\n");

  const copied = await copyLaunchCommand(commands);
  if (copied) {
    setLaunchStatus("All client commands copied", "Client 1~4 commands copied. Paste them into PowerShell or enable custom protocol mode for click-to-launch.", "warning");
  }
}
async function startClient(client) {
  const label = client && client.label ? client.label : "Client";
  if (launcherConfig.customProtocolEnabled) {
    const protocolUrl = client && client.protocolUrl ? client.protocolUrl : launcherConfig.customProtocolUrl || "projectdawn://launch";
    setLaunchStatus(`${label} launch requested`, `${label} launch requested. The browser may show a security confirmation prompt.`, "info");
    window.location.href = protocolUrl;
    return;
  }

  const copied = await copyLaunchCommand(client && client.startCommand);
  if (copied) {
    setLaunchStatus(`${label} command copied`, `${label} command copied. Paste it into PowerShell or the Windows Run dialog, or enable custom protocol mode for click-to-launch.`, "warning");
  }
}

function toggleCustomProtocol(enabled) {
  launcherConfig.customProtocolEnabled = enabled;
  renderLaunchMode();
  if (enabled) setLaunchStatus("Ready", "Custom protocol mode is enabled. Start Game and Client buttons will request projectdawn:// launch URLs.", "info");
  else setLaunchStatus("Custom protocol is disabled", "Copy command mode is enabled. Start Game will copy the launch command.", "warning");
}

function openSetupGuide() {
  window.open("README.md", "_blank", "noopener");
}

function setAutoRefresh(enabled) {
  if (autoRefreshTimer) {
    clearInterval(autoRefreshTimer);
    autoRefreshTimer = null;
  }
  if (enabled) autoRefreshTimer = setInterval(refreshAll, REFRESH_INTERVAL_MS);
}

renderLauncherConfig();
els.apiBaseText.textContent = API_BASE_URL;
els.refreshButton.addEventListener("click", refreshAll);
els.autoRefreshToggle.addEventListener("change", event => setAutoRefresh(event.target.checked));
els.startGameButton.addEventListener("click", startGame);
els.startAllClientsButton.addEventListener("click", startAllClients);
els.copyLaunchCommandButton.addEventListener("click", () => copyLaunchCommand());
els.openSetupGuideButton.addEventListener("click", openSetupGuide);
els.customProtocolToggle.addEventListener("change", event => toggleCustomProtocol(event.target.checked));
setAutoRefresh(els.autoRefreshToggle.checked);
refreshAll();









