mergeInto(LibraryManager.library, {
  // Transport only. All visible chat, names and speech bubbles render inside Unity.
  $KimchilyChat: {
    socket: null, receiver: '',
    call: function (method, value) { SendMessage(KimchilyChat.receiver, method, value || ''); },
    close: function () {
      var socket = KimchilyChat.socket; KimchilyChat.socket = null;
      if (socket) { socket.onopen = socket.onmessage = socket.onclose = socket.onerror = null; socket.close(); }
    }
  },
  KimchilyChat_Create__deps: ['$KimchilyChat'],
  KimchilyChat_Create: function (receiver) {
    var chat = KimchilyChat; chat.receiver = UTF8ToString(receiver);
    chat.onHide = function () { chat.call('Disconnect'); chat.close(); };
    window.addEventListener('pagehide', chat.onHide);
  },
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
      socket.onerror = function () { chat.close(); wire('closed', '채팅 서버에 연결하지 못했습니다. 다시 연결해 주세요.'); };
    } catch (error) { wire('closed', '서버 주소를 확인해 주세요. HTTPS에서는 WSS가 필요합니다.'); }
  },
  KimchilyChat_Send__deps: ['$KimchilyChat'],
  KimchilyChat_Send: function (json) {
    var socket = KimchilyChat.socket;
    if (socket && socket.readyState === WebSocket.OPEN && socket.bufferedAmount < 65536) socket.send(UTF8ToString(json));
  },
  KimchilyChat_Close__deps: ['$KimchilyChat'],
  KimchilyChat_Close: function () { KimchilyChat.close(); },
  KimchilyChat_Destroy__deps: ['$KimchilyChat'],
  KimchilyChat_Destroy: function () { KimchilyChat.close(); window.removeEventListener('pagehide', KimchilyChat.onHide); }
});
