'use strict';
const element = id => document.getElementById(id);
const fields = ['name', 'room', 'world', 'revision'];
const query = new URLSearchParams(location.search);
for (const id of ['room', 'world', 'revision']) {
  const value = query.get(id);
  if (value && /^[A-Za-z0-9_-]{1,64}$/.test(value)) element(id).value = value;
}
let socket = null;
let selfId = null;
let joined = false;
let connecting = false;
const players = new Map();
function notice(text) { element('notice').textContent = text; }
function controls() {
  document.body.classList.toggle('in-room', joined);
  for (const id of fields) element(id).disabled = joined || connecting;
  element('join').disabled = joined || connecting;
  element('leave').disabled = !joined;
  element('message').disabled = !joined;
  element('send').disabled = !joined;
  element('connection').textContent = joined ? '함께 연결됨' : connecting ? '연결 중' : '연결 전';
  element('connection').classList.toggle('online', joined);
}
function drawPlayers() {
  element('players').replaceChildren();
  for (const player of players.values()) {
    const item = document.createElement('li');
    item.textContent = player.name + (player.playerId === selfId ? ' · 나' : '');
    element('players').append(item);
  }
  element('count').textContent = `${players.size} / 8`;
}
function addLine(text, chat = null) {
  element('empty-chat')?.remove();
  const line = document.createElement('div');
  if (chat) {
    line.className = 'chat-line' + (chat.playerId === selfId ? ' mine' : '');
    const meta = document.createElement('div');
    meta.className = 'chat-meta';
    meta.textContent = `${chat.name} · ${new Date(chat.sentAtUtc).toLocaleTimeString('ko-KR', { hour: '2-digit', minute: '2-digit' })}`;
    const body = document.createElement('div');
    body.className = 'chat-body';
    body.textContent = text;
    line.append(meta, body);
  } else { line.className = 'system-line'; line.textContent = text; }
  element('messages').append(line);
  while (element('messages').children.length > 100) element('messages').firstElementChild.remove();
  element('messages').scrollTop = element('messages').scrollHeight;
}
function reset() {
  joined = connecting = false;
  selfId = null;
  players.clear();
  drawPlayers();
  controls();
  element('room-title').textContent = '만나면 인사해요';
}
function send(type, payload = {}) {
  if (socket?.readyState !== WebSocket.OPEN) { notice('연결을 확인한 뒤 다시 입장해 주세요.'); return false; }
  socket.send(JSON.stringify({ protocolVersion: 1, type, ...payload }));
  return true;
}
element('join-form').addEventListener('submit', event => {
  event.preventDefault();
  if (joined || connecting) return;
  const payload = { worldId: element('world').value, revisionId: element('revision').value, roomId: element('room').value, name: element('name').value.trim() };
  if (!payload.name) { notice('닉네임을 입력해 주세요.'); return; }
  socket?.close();
  connecting = true;
  controls();
  const active = new WebSocket(`${location.protocol === 'https:' ? 'wss:' : 'ws:'}//${location.host}/ws`);
  socket = active;
  const timeout = setTimeout(() => {
    if (socket === active && connecting) { active.close(); notice('연결 시간이 초과됐어요. 다시 입장해 주세요.'); }
  }, 10000);
  active.addEventListener('open', () => { if (socket === active) send('join', payload); });
  active.addEventListener('message', event => {
    if (socket !== active) return;
    let value;
    try { value = JSON.parse(event.data); } catch { notice('서버 응답을 읽을 수 없어요.'); active.close(); return; }
    switch (value.type) {
      case 'joined':
        clearTimeout(timeout);
        joined = true; connecting = false; selfId = value.selfId;
        players.clear();
        for (const player of value.players) players.set(player.playerId, player);
        element('messages').replaceChildren();
        for (const chat of value.history) addLine(chat.text, chat);
        addLine('방에 입장했어요. 함께할 사람에게 인사해 보세요.');
        element('room-title').textContent = value.room.roomId;
        notice('초대 주소를 다른 기기에 공유해 보세요.');
        drawPlayers(); controls(); break;
      case 'playerJoined':
        players.set(value.player.playerId, value.player);
        addLine(`${value.player.name} 님이 들어왔어요.`); drawPlayers(); break;
      case 'playerLeft':
        players.delete(value.player.playerId);
        addLine(`${value.player.name} 님이 나갔어요.`); drawPlayers(); break;
      case 'chat': addLine(value.chat.text, value.chat); break;
      case 'left': reset(); addLine('방에서 나왔어요.'); active.close(); break;
      case 'error':
        notice(value.message);
        if (!joined) { clearTimeout(timeout); reset(); active.close(); }
        break;
    }
  });
  active.addEventListener('close', () => {
    clearTimeout(timeout);
    if (socket !== active) return;
    const wasJoined = joined;
    reset();
    if (wasJoined) { addLine('연결이 종료됐어요. 다시 입장할 수 있어요.'); notice('다시 입장하면 새 참가자로 연결됩니다.'); }
  });
  active.addEventListener('error', () => { if (socket === active) notice('서버에 연결할 수 없어요. 주소와 실행 상태를 확인해 주세요.'); });
});
element('chat-form').addEventListener('submit', event => {
  event.preventDefault();
  const text = element('message').value.trim();
  if (joined && text && send('chat', { text })) element('message').value = '';
});
element('leave').addEventListener('click', () => send('leave'));
element('invite').addEventListener('click', async () => {
  const url = new URL('/', location.origin);
  for (const id of ['room', 'world', 'revision']) url.searchParams.set(id, element(id).value);
  element('invite-url').hidden = false;
  element('invite-url').value = url.href;
  element('invite-url').select();
  try { await navigator.clipboard.writeText(url.href); notice('초대 주소를 복사했어요.'); }
  catch { notice('선택된 초대 주소를 복사해 공유해 주세요.'); }
  if (['127.0.0.1', 'localhost'].includes(location.hostname)) notice('다른 기기에는 PC의 LAN IP로 접속한 주소를 공유해 주세요.');
});
window.addEventListener('pagehide', () => socket?.close());
controls();
