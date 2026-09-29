using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Kimchily.Creator.Mobile;
using UnityEngine;
using UnityEngine.Scripting;

namespace Kimchily.Networking
{
    [Preserve, DefaultExecutionOrder(-200), DisallowMultipleComponent]
    public sealed class InGameChat : MonoBehaviour
    {
        public static InGameChat Instance { get; private set; }
        public ChatView View { get; } = new ChatView();
        readonly ConcurrentQueue<ChatWireEvent> incoming = new ConcurrentQueue<ChatWireEvent>();
        readonly List<KimchilyMobileControls> pausedControls = new List<KimchilyMobileControls>();
        readonly List<ChatLine> lines = new List<ChatLine>();
        readonly List<ChatPlayer> players = new List<ChatPlayer>();
        int generation;
        float deadline;
        bool active;
        string draft = "";
        Vector2 scroll;
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void KimchilyChat_Create(string receiver);
        [DllImport("__Internal")] static extern void KimchilyChat_Render(string json);
        [DllImport("__Internal")] static extern void KimchilyChat_Open(string endpoint, int generation);
        [DllImport("__Internal")] static extern void KimchilyChat_Send(string json);
        [DllImport("__Internal")] static extern void KimchilyChat_Close();
        [DllImport("__Internal")] static extern void KimchilyChat_Destroy();
#else
        NativeChatSocket socket;
#endif
        public static InGameChat Ensure()
        {
            if (Instance == null) new GameObject("KimchilyInGameChat").AddComponent<InGameChat>();
            return Instance;
        }
        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; gameObject.name = "KimchilyInGameChat";
            DontDestroyOnLoad(gameObject);
            View.worldId = "lobby"; View.revisionId = "v1";
            View.settings.name = "Guest-" + UnityEngine.Random.Range(100, 1000);
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLInput.captureAllKeyboardInput = false;
            KimchilyChat_Create(gameObject.name);
#endif
            Render();
        }
        public void SetContext(string worldId, string revisionId)
        {
            if (View.worldId == worldId && View.revisionId == revisionId) return;
            bool reconnect = active;
            Disconnect("");
            View.worldId = worldId; View.revisionId = revisionId;
            View.status = worldId == "lobby" ? "로비 채팅 · 매칭 전에 대화해 보세요." : "월드 채팅 · 같은 방의 플레이어와 대화하세요.";
            ApplyInputPause(); Render();
            if (reconnect) Connect(JsonUtility.ToJson(View.settings));
        }
        [Preserve] public void ConfigureEndpoint(string endpoint)
        {
            if (!active && ChatValidation.IsEndpoint(endpoint)) { View.settings.endpoint = endpoint; Render(); }
        }
        [Preserve] public void Connect(string json)
        {
            if (active) return;
            ChatSettings settings;
            try { settings = JsonUtility.FromJson<ChatSettings>(json); }
            catch (Exception) { View.status = "연결 설정을 확인해 주세요."; Render(); return; }
            if (settings == null || !ChatValidation.IsEndpoint(settings.endpoint) || !ChatValidation.IsId(settings.roomId)
                || !ChatValidation.IsText(settings.name, 24) || !ChatValidation.IsId(View.worldId) || !ChatValidation.IsId(View.revisionId))
            { View.status = "주소, 닉네임(1–24자), 방 코드(영문·숫자·_- 1–64자)를 확인해 주세요."; Render(); return; }
            Disconnect(""); View.settings = settings; active = true; View.connecting = true;
            View.status = "채팅방에 연결 중…"; deadline = Time.realtimeSinceStartup + 12;
#if UNITY_WEBGL && !UNITY_EDITOR
            KimchilyChat_Open(settings.endpoint, generation);
#else
            socket = new NativeChatSocket(settings.endpoint, generation, incoming);
#endif
            Render();
        }
        [Preserve] public void Disconnect(string unused)
        {
            generation++; active = false;
#if UNITY_WEBGL && !UNITY_EDITOR
            KimchilyChat_Close();
#else
            socket?.Dispose(); socket = null;
#endif
            View.joined = false; View.connecting = false; View.selfId = "";
            lines.Clear(); players.Clear(); draft = "";
            View.status = "채팅방 연결을 종료했습니다.";
            Render();
        }
        [Preserve] public void SetExpanded(string value)
        {
            View.expanded = value == "true";
            ApplyInputPause(); Render();
        }
        void ApplyInputPause()
        {
            foreach (var controls in pausedControls) if (controls != null) { controls.ClearInput(); controls.enabled = true; }
            pausedControls.Clear();
            if (!View.expanded) return;
            foreach (var controls in FindObjectsByType<KimchilyMobileControls>(FindObjectsSortMode.None))
                if (controls.enabled) { controls.ClearInput(); controls.enabled = false; pausedControls.Add(controls); }
        }
        [Preserve] public void SendChat(string text)
        {
            if (!View.joined || !ChatValidation.IsText(text, 300)) return;
            Send(new ChatCommand { type = "chat", text = text });
        }
        void Send(ChatCommand command)
        {
            string json = JsonUtility.ToJson(command);
#if UNITY_WEBGL && !UNITY_EDITOR
            KimchilyChat_Send(json);
#else
            socket?.Send(json);
#endif
        }
        [Preserve] public void OnWire(string json)
        {
            try
            {
                var wire = JsonUtility.FromJson<ChatWireEvent>(json);
                if (wire != null && incoming.Count < 128) incoming.Enqueue(wire);
                else if (active) Disconnect("");
            }
            catch (Exception) { if (active) Disconnect(""); }
        }
        void Update()
        {
            if (View.connecting && Time.realtimeSinceStartup >= deadline)
            { Disconnect(""); View.status = "연결 시간이 초과되었습니다. 서버 주소를 확인해 주세요."; Render(); }
            for (int i = 0; i < 32 && incoming.TryDequeue(out var wire); i++)
            {
                if (!active || wire.generation != generation) continue;
                if (wire.type == "open") Send(new ChatCommand { type = "join", worldId = View.worldId, revisionId = View.revisionId, roomId = View.settings.roomId, name = View.settings.name });
                else if (wire.type == "closed")
                { Disconnect(""); View.status = string.IsNullOrEmpty(wire.data) ? "연결이 종료되었습니다. 다시 연결할 수 있습니다." : wire.data; Render(); }
                else if (wire.type == "message")
                {
                    try { Handle(JsonUtility.FromJson<ChatEvent>(wire.data)); }
                    catch (Exception) { Disconnect(""); View.status = "서버 응답 형식을 확인해 주세요."; Render(); }
                }
            }
        }
        void Handle(ChatEvent message)
        {
            if (message == null || message.protocolVersion != 1) throw new InvalidOperationException();
            switch (message.type)
            {
                case "hello": View.selfId = message.selfId; break;
                case "joined":
                    if (message.room == null || message.room.worldId != View.worldId || message.room.revisionId != View.revisionId || message.room.roomId != View.settings.roomId) throw new InvalidOperationException();
                    View.joined = true; View.connecting = false; View.status = "채팅 연결됨";
                    players.Clear(); if (message.players != null) players.AddRange(message.players);
                    lines.Clear(); if (message.history != null) lines.AddRange(message.history); break;
                case "playerJoined": if (message.player != null && !players.Exists(p => p.playerId == message.player.playerId)) players.Add(message.player); break;
                case "playerLeft": if (message.player != null) players.RemoveAll(p => p.playerId == message.player.playerId); break;
                case "chat": if (message.chat != null) { lines.Add(message.chat); if (lines.Count > 100) lines.RemoveAt(0); scroll.y = float.MaxValue; } break;
                case "error":
                    if (!View.joined) Disconnect("");
                    View.status = message.message; break;
            }
            Render();
        }
        void Render()
        {
            View.players = players.ToArray(); View.messages = lines.ToArray();
#if UNITY_WEBGL && !UNITY_EDITOR
            if (Instance == this) KimchilyChat_Render(JsonUtility.ToJson(View));
#endif
        }
#if !UNITY_WEBGL || UNITY_EDITOR
        void OnGUI()
        {
            var oldMatrix = GUI.matrix;
            float scale = Mathf.Clamp(Screen.width / 700f, 1, 2);
            GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
            GUI.depth = -200;
            float width = Screen.width / scale;
            if (GUI.Button(new Rect(width - 160, 62, 145, 36), View.expanded ? "Close chat" : "Chat · " + players.Count)) SetExpanded(View.expanded ? "false" : "true");
            if (View.expanded)
            {
                GUILayout.BeginArea(new Rect(Mathf.Max(8, width - 390), 105, Mathf.Min(380, width - 16), Mathf.Max(220, Screen.height / scale - 130)), GUI.skin.box);
                GUILayout.Label("KIMCHILY · " + View.worldId + " / " + View.revisionId);
                GUILayout.Label(View.status);
                if (!active)
                {
                    GUILayout.Label("Server WebSocket URL"); View.settings.endpoint = GUILayout.TextField(View.settings.endpoint, 256);
                    GUILayout.Label("Nickname"); View.settings.name = GUILayout.TextField(View.settings.name, 24);
                    GUILayout.Label("Room code"); View.settings.roomId = GUILayout.TextField(View.settings.roomId, 64);
                    if (GUILayout.Button("Join chat", GUILayout.Height(32))) Connect(JsonUtility.ToJson(View.settings));
                }
                else
                {
                    GUILayout.Label("Room: " + View.settings.roomId + " · " + players.Count + "/8");
                    scroll = GUILayout.BeginScrollView(scroll);
                    var plain = new GUIStyle(GUI.skin.label) { wordWrap = true, richText = false };
                    foreach (var line in lines) GUILayout.Label(line.name + ": " + line.text, plain);
                    GUILayout.EndScrollView();
                    GUI.enabled = View.joined;
                    draft = GUILayout.TextField(draft, 300);
                    if (GUILayout.Button("Send", GUILayout.Height(32))) { SendChat(draft); draft = ""; GUI.FocusControl(null); }
                    GUI.enabled = true;
                    if (GUILayout.Button("Leave chat")) Disconnect("");
                }
                GUILayout.EndArea();
            }
            GUI.matrix = oldMatrix;
        }
#endif
        void OnDisable()
        {
            if (Instance != this) return;
            View.expanded = false; ApplyInputPause(); Disconnect("");
        }
        void OnDestroy()
        {
            if (Instance != this) return;
            View.expanded = false; ApplyInputPause(); Disconnect("");
#if UNITY_WEBGL && !UNITY_EDITOR
            KimchilyChat_Destroy();
#endif
            Instance = null;
        }
    }
}
