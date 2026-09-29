mergeInto(LibraryManager.library, {
  $KimchilyChat: {
    root: null, ui: null, socket: null, receiver: '', state: null,
    call: function (method, value) { SendMessage(KimchilyChat.receiver, method, value || ''); },
    close: function () {
      var socket = KimchilyChat.socket; KimchilyChat.socket = null;
      if (socket) { socket.onopen = socket.onmessage = socket.onclose = socket.onerror = null; socket.close(); }
    },
    render: function (state) {
      var chat = KimchilyChat, ui = chat.ui;
      if (!ui) return;
      chat.state = state;
      ui.panel.hidden = !state.expanded;
      ui.toggle.textContent = state.expanded ? '채팅 닫기 ×' : '채팅 · ' + state.players.length;
      ui.toggle.setAttribute('aria-expanded', String(state.expanded));
      ui.context.textContent = (state.worldId === 'lobby' ? '매칭 전 로비' : '월드 채팅') + ' · ' + state.worldId;
      ui.revision.textContent = state.revisionId + ' / ' + (state.joined ? state.settings.roomId : '방 코드로 연결');
      ui.status.textContent = state.status;
      ui.form.hidden = state.joined || state.connecting;
      ui.leave.hidden = !state.joined && !state.connecting;
      ui.compose.hidden = !state.joined;
      ui.people.textContent = state.joined ? state.players.map(function (p) { return p.name; }).join(' · ') + '  (' + state.players.length + '/8)' : '';
      if (!chat.initialized) {
        ui.endpoint.value = (location.protocol === 'https:' ? 'wss://' : 'ws://') + location.hostname + ':8790/ws';
        ui.nickname.value = state.settings.name; ui.room.value = state.settings.roomId; chat.initialized = true;
      }
      var key = state.messages.map(function (m) { return m.id; }).join(',');
      if (key !== chat.messageKey) {
        chat.messageKey = key; ui.messages.replaceChildren();
        state.messages.forEach(function (m) {
          var item = document.createElement('li'), name = document.createElement('strong'), text = document.createElement('span');
          name.textContent = m.name + (m.playerId === state.selfId ? ' · 나' : ''); text.textContent = m.text;
          item.append(name, text); ui.messages.append(item);
        });
        ui.messages.scrollTop = ui.messages.scrollHeight;
      }
    }
  },
  KimchilyChat_Create__deps: ['$KimchilyChat'],
  KimchilyChat_Create: function (receiver) {
    var chat = KimchilyChat; chat.receiver = UTF8ToString(receiver);
    if (chat.root) return;
    var root = document.createElement('div'); root.id = 'kimchily-ingame-chat';
    var shadow = root.attachShadow({ mode: 'open' });
    // Constant template only. Player names, chat messages and server state use textContent.
    shadow.innerHTML = '<style>' +
      ':host{position:fixed;z-index:1000;top:74px;right:max(14px,env(safe-area-inset-right));font:14px/1.5 system-ui,sans-serif;color:#f3f7f5;width:min(360px,calc(100vw - 28px));pointer-events:none}' +
      '*{box-sizing:border-box}[hidden]{display:none!important}button,input{font:inherit}button{cursor:pointer;border:1px solid #415452;border-radius:10px;padding:9px 14px;background:#203937;color:#f3f7f5;min-height:40px}' +
      'button:focus-visible,input:focus-visible{outline:2px solid #c8f794;outline-offset:2px}#toggle{display:block;margin-left:auto;background:#163b32;pointer-events:auto;box-shadow:0 4px 18px #0004}' +
      '#panel{pointer-events:auto;display:flex;flex-direction:column;gap:10px;margin-top:8px;padding:16px;background:#0d2424f5;border:1px solid #46625b;border-radius:16px;box-shadow:0 12px 50px #0007;max-height:calc(var(--chat-height,100dvh) - 142px);overflow:auto}' +
      'header{display:flex;justify-content:space-between;gap:8px}h2{font-size:16px;margin:0}small{color:#acc8bd;overflow-wrap:anywhere}#status{color:#c8f794;margin:0;font-size:12px}#people{font-size:12px;color:#b0c4be;overflow-wrap:anywhere}' +
      'form{display:flex;flex-direction:column;gap:9px}label{font-size:12px;color:#bccfc9;display:flex;flex-direction:column;gap:4px}input{background:#091c1f;color:white;border:1px solid #416059;border-radius:8px;padding:9px;min-width:0;width:100%;font-size:16px}' +
      '#join,#send{background:#c8f794;color:#143423;font-weight:700;border:0}#leave{font-size:12px;align-self:flex-start;padding:5px 9px;min-height:30px}' +
      'ul{padding:0;margin:0;list-style:none;min-height:80px;overflow:auto;flex:1}li{padding:8px 10px;margin-bottom:7px;border-radius:10px;background:#1a3938;overflow-wrap:anywhere}li strong{display:block;color:#b3d9c2;font-size:11px}li span{white-space:pre-wrap}' +
      '#compose{flex-direction:row}#compose label{flex:1}#send{align-self:end}#empty{color:#91aba4;font-size:12px;margin:0}ul:empty{display:none}ul:not(:empty)+#empty{display:none}' +
      '</style><button id="toggle" aria-expanded="false" aria-controls="panel">채팅 · 0</button>' +
      '<section id="panel" hidden aria-label="인게임 채팅"><header><div><h2 id="context">월드 채팅</h2><small id="revision"></small></div></header>' +
      '<p id="status" role="status"></p><form id="form"><label>내 닉네임<input id="nickname" maxlength="24" required autocomplete="nickname"></label>' +
      '<label>방 코드<input id="room" value="playground" maxlength="64" pattern="[A-Za-z0-9_-]+" required></label>' +
      '<details><summary>서버 연결 설정</summary><label>WebSocket 주소<input id="endpoint" type="url" required spellcheck="false"></label></details>' +
      '<button id="join" type="submit">채팅방 입장</button></form><div id="people"></div>' +
      '<ul id="messages" role="log" aria-label="대화 내용" aria-live="polite"></ul><p id="empty">같은 월드·버전·방 코드의 친구와 대화합니다.</p>' +
      '<form id="compose" hidden><label>메시지<input id="draft" maxlength="300" required autocomplete="off" enterkeyhint="send"></label><button id="send">전송</button></form>' +
      '<button id="leave" hidden>채팅방 나가기</button></section>';
    var ui = {};
    ['toggle','panel','context','revision','status','form','nickname','room','endpoint','join','people','messages','compose','draft','leave'].forEach(function (id) { ui[id] = shadow.getElementById(id); });
    chat.root = root; chat.ui = ui; chat.messageKey = null; chat.initialized = false;
    ui.toggle.addEventListener('click', function () { chat.call('SetExpanded', String(!chat.state.expanded)); });
    ui.form.addEventListener('submit', function (event) {
      event.preventDefault();
      chat.call('Connect', JSON.stringify({ endpoint: ui.endpoint.value.trim(), name: ui.nickname.value.trim(), roomId: ui.room.value.trim() }));
    });
    ui.compose.addEventListener('submit', function (event) {
      event.preventDefault();
      if (ui.draft.value.trim()) { chat.call('SendChat', ui.draft.value.trim()); ui.draft.value = ''; }
    });
    ui.draft.addEventListener('keydown', function (event) { if (event.key === 'Enter' && event.isComposing) event.preventDefault(); });
    ui.leave.addEventListener('click', function () { chat.call('Disconnect'); });
    ['keydown','keyup','pointerdown','pointerup','touchstart','touchend','wheel'].forEach(function (name) { root.addEventListener(name, function (event) { event.stopPropagation(); }); });
    chat.onResize = function () { root.style.setProperty('--chat-height', (window.visualViewport ? window.visualViewport.height : window.innerHeight) + 'px'); };
    if (window.visualViewport) window.visualViewport.addEventListener('resize', chat.onResize);
    chat.onHide = function () { chat.close(); }; window.addEventListener('pagehide', chat.onHide);
    (document.getElementById('player') || document.body).appendChild(root); chat.onResize();
  },
  KimchilyChat_Render__deps: ['$KimchilyChat'],
  KimchilyChat_Render: function (json) { KimchilyChat.render(JSON.parse(UTF8ToString(json))); },
  KimchilyChat_Open__deps: ['$KimchilyChat'],
  KimchilyChat_Open: function (endpoint, generation) {
    var chat = KimchilyChat; chat.close();
    var wire = function (type, data) { chat.call('OnWire', JSON.stringify({ generation: generation, type: type, data: data || '' })); };
    try {
      var socket = new WebSocket(UTF8ToString(endpoint)); chat.socket = socket;
      socket.onopen = function () { wire('open'); };
      socket.onmessage = function (event) {
        if (typeof event.data !== 'string' || event.data.length > 65536) { chat.close(); wire('closed', '서버 메시지 크기가 너무 큽니다.'); return; }
        wire('message', event.data);
      };
      socket.onclose = function () { if (chat.socket === socket) chat.socket = null; wire('closed'); };
      socket.onerror = function () { chat.close(); wire('closed', '서버 연결을 확인해 주세요. HTTPS에서는 WSS 주소가 필요합니다.'); };
    } catch (error) { wire('closed', '서버 주소를 확인해 주세요. HTTPS에서는 WSS 주소가 필요합니다.'); }
  },
  KimchilyChat_Send__deps: ['$KimchilyChat'],
  KimchilyChat_Send: function (json) {
    var socket = KimchilyChat.socket;
    if (socket && socket.readyState === WebSocket.OPEN && socket.bufferedAmount < 65536) socket.send(UTF8ToString(json));
  },
  KimchilyChat_Close__deps: ['$KimchilyChat'],
  KimchilyChat_Close: function () { KimchilyChat.close(); },
  KimchilyChat_Destroy__deps: ['$KimchilyChat'],
  KimchilyChat_Destroy: function () {
    var chat = KimchilyChat; chat.close();
    if (window.visualViewport) window.visualViewport.removeEventListener('resize', chat.onResize);
    window.removeEventListener('pagehide', chat.onHide);
    if (chat.root) chat.root.remove(); chat.root = chat.ui = null;
  }
});
