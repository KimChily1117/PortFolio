using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Kimchily.Creator.Mobile;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;

namespace Kimchily.Networking
{
    [Preserve, DefaultExecutionOrder(-200), DisallowMultipleComponent]
    public sealed class InGameChat : MonoBehaviour
    {
        public static InGameChat Instance { get; private set; }
        public ChatView View { get; } = new ChatView();

        public event Action Changed;
        public event Action<ChatEvent> ServerEvent;

        public NetworkAvatars Avatars { get; private set; }
        public ScriptGameClient Game { get; private set; }
        public KimchilyMobilePlayer LocalPlayer { get; private set; }

        readonly ConcurrentQueue<ChatWireEvent> incoming = new ConcurrentQueue<ChatWireEvent>();
        readonly List<KimchilyMobileControls> pausedControls = new List<KimchilyMobileControls>();
        readonly List<ChatLine> lines = new List<ChatLine>();
        readonly List<ChatPlayer> players = new List<ChatPlayer>();
        int generation;
        long sequence;
        float deadline;
        float nextPose;
        bool active;
        bool configured;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        static extern void KimchilyChat_Create(string receiver);

        [DllImport("__Internal")]
        static extern void KimchilyChat_Open(string endpoint, int generation);

        [DllImport("__Internal")]
        static extern void KimchilyChat_Send(string json);

        [DllImport("__Internal")]
        static extern void KimchilyChat_Close();

        [DllImport("__Internal")]
        static extern void KimchilyChat_Destroy();
#else
        NativeChatSocket socket;
#endif

        public static InGameChat Ensure()
        {
            if (Instance == null)
            {
                new GameObject("KimchilyInGameChat").AddComponent<InGameChat>();
            }

            return Instance;
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            gameObject.name = "KimchilyInGameChat";
            DontDestroyOnLoad(gameObject);
            View.worldId = "lobby";
            View.revisionId = "v1";
            View.settings.name = "";
            gameObject.AddComponent<UnityChatPanel>().Initialize(this);
            Avatars = gameObject.AddComponent<NetworkAvatars>();
            Avatars.Initialize(this);
            Game = gameObject.AddComponent<ScriptGameClient>();
            Game.Initialize(this);
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLInput.captureAllKeyboardInput = false;
            KimchilyChat_Create(gameObject.name);
#endif
            Render();
        }

        [Preserve]
        public void ConfigureSession(string json)
        {
            ChatSettings settings;
            try
            {
                settings = JsonUtility.FromJson<ChatSettings>(json);
            }
            catch (Exception)
            {
                Status("입장 설정을 확인해 주세요.");
                return;
            }

            if (!ValidSettings(settings))
            {
                Status("닉네임과 서버 연결 설정을 확인해 주세요.");
                return;
            }

            if (active)
            {
                Disconnect("");
            }

            View.settings = settings;
            configured = true;
            Render();
            if (LocalPlayer != null)
            {
                Connect(json);
            }
        }

        static bool ValidSettings(ChatSettings settings)
        {
            return settings != null
                && ChatValidation.IsEndpoint(settings.endpoint)
                && ChatValidation.IsId(settings.roomId)
                && ChatValidation.IsText(settings.name, 24);
        }

        public void EnterWorld(Scene scene, string worldId, string revisionId)
        {
            SetContext(worldId, revisionId);
            Game.EnterWorld(worldId, revisionId);
            LocalPlayer = KimchilyMobilePlayerBootstrap.EnsureForScene(scene);
            Avatars.Bind(LocalPlayer);
            ApplyInputPause();
            if (configured)
            {
                Connect(JsonUtility.ToJson(View.settings));
            }
            else
            {
                Status("입장 닉네임을 설정해 주세요.");
            }
        }

        public void ExitWorld()
        {
            Game.ResetWorld();
            Disconnect("");
            Avatars.Clear();
            LocalPlayer = null;
            SetExpanded("false");
            SetContext("lobby", "v1");
        }

        public void SetContext(string worldId, string revisionId)
        {
            if (View.worldId != worldId || View.revisionId != revisionId)
            {
                Disconnect("");
                Avatars?.Clear();
                LocalPlayer = null;
                View.worldId = worldId;
                View.revisionId = revisionId;
                View.status = worldId == "lobby" ? "월드에 입장하면 자동으로 연결합니다." : "월드 채팅을 준비합니다.";
            }

            ApplyInputPause();
            Render();
        }

        [Preserve]
        public void ConfigureEndpoint(string endpoint)
        {
            if (!active && ChatValidation.IsEndpoint(endpoint))
            {
                View.settings.endpoint = endpoint;
                Render();
            }
        }

        [Preserve]
        public void Connect(string json)
        {
            if (active)
            {
                return;
            }

            ChatSettings settings;
            try
            {
                settings = JsonUtility.FromJson<ChatSettings>(json);
            }
            catch (Exception)
            {
                Status("연결 설정을 확인해 주세요.");
                return;
            }

            if (!ValidSettings(settings) || !ChatValidation.IsId(View.worldId) || !ChatValidation.IsId(View.revisionId))
            {
                Status("닉네임(1–24자), 서버 주소, 방 코드를 확인해 주세요.");
                return;
            }

            Disconnect("");
            View.settings = settings;
            configured = true;
            active = true;
            View.connecting = true;
            View.status = "월드 채팅 연결 중…";
            deadline = Time.realtimeSinceStartup + 12;
            sequence = 0;
#if UNITY_WEBGL && !UNITY_EDITOR
            KimchilyChat_Open(settings.endpoint, generation);
#else
            socket = new NativeChatSocket(settings.endpoint, generation, incoming);
#endif
            Render();
        }

        [Preserve]
        public void Disconnect(string unused)
        {
            generation++;
            active = false;
#if UNITY_WEBGL && !UNITY_EDITOR
            KimchilyChat_Close();
#else
            socket?.Dispose();
            socket = null;
#endif
            View.joined = false;
            View.connecting = false;
            View.selfId = "";
            lines.Clear();
            players.Clear();
            Avatars?.ClearParticipants();
            View.status = "연결 종료 · 다시 연결할 수 있습니다.";
            Render();
        }

        [Preserve]
        public void SetExpanded(string value)
        {
            View.expanded = value == "true";
            ApplyInputPause();
            Render();
        }

        void ApplyInputPause()
        {
            foreach (var controls in pausedControls)
            {
                if (controls != null)
                {
                    controls.ClearInput();
                    controls.enabled = true;
                }
            }

            pausedControls.Clear();
            if (!View.expanded)
            {
                return;
            }

            foreach (var controls in FindObjectsByType<KimchilyMobileControls>(FindObjectsSortMode.None))
            {
                if (controls.enabled)
                {
                    controls.ClearInput();
                    controls.enabled = false;
                    pausedControls.Add(controls);
                }
            }
        }

        [Preserve]
        public void SendChat(string text)
        {
            text = text?.Trim();
            if (!View.joined || !ChatValidation.IsText(text, 300))
            {
                return;
            }

            Send(new ChatCommand { type = "chat", text = text });
        }

        internal bool SendGameCommand(string scriptId, string scriptHash, string action, string payloadJson)
        {
            if (!View.joined
                || !ScriptGameClient.IsValidIdentity(scriptId, scriptHash)
                || !ChatValidation.IsId(action)
                || string.IsNullOrWhiteSpace(payloadJson)
                || payloadJson.Length > ScriptGameClient.MaximumPayloadCharacters)
            {
                return false;
            }

            var json = JsonUtility.ToJson(new ChatCommand
            {
                type = "game",
                scriptId = scriptId,
                scriptHash = scriptHash,
                action = action,
                payloadJson = payloadJson
            });
            // payload의 글자 수만으로 한글 UTF-8 및 JSON 이스케이프 비용을 예측할 수 없다.
            // 서버의 4 KiB 수신 한도와 동일하게 최종 전송 바이트를 확인한다.
            if (Encoding.UTF8.GetByteCount(json) > 4096)
            {
                return false;
            }

            SendJson(json);
            return true;
        }

        void Send(ChatCommand command)
        {
            SendJson(JsonUtility.ToJson(command));
        }

        void SendJson(string json)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            KimchilyChat_Send(json);
#else
            socket?.Send(json);
#endif
        }

        [Preserve]
        public void OnWire(string json)
        {
            try
            {
                var wire = JsonUtility.FromJson<ChatWireEvent>(json);
                if (wire != null && incoming.Count < 128)
                {
                    incoming.Enqueue(wire);
                }
                else if (active)
                {
                    Disconnect("");
                }
            }
            catch (Exception)
            {
                if (active)
                {
                    Disconnect("");
                }
            }
        }

        void Update()
        {
            if (View.connecting && Time.realtimeSinceStartup >= deadline)
            {
                Disconnect("");
                Status("연결 시간이 초과되었습니다. 다시 연결을 눌러 주세요.");
            }

            for (int i = 0; i < 48 && incoming.TryDequeue(out var wire); i++)
            {
                if (!active || wire.generation != generation)
                {
                    continue;
                }

                if (wire.type == "open")
                {
                    Send(new ChatCommand
                    {
                        type = "join",
                        worldId = View.worldId,
                        revisionId = View.revisionId,
                        roomId = View.settings.roomId,
                        name = View.settings.name
                    });
                }
                else if (wire.type == "closed")
                {
                    Disconnect("");
                    Status(string.IsNullOrEmpty(wire.data) ? "연결이 끊겼습니다. 다시 연결을 눌러 주세요." : wire.data);
                }
                else if (wire.type == "message")
                {
                    try
                    {
                        Handle(JsonUtility.FromJson<ChatEvent>(wire.data));
                    }
                    catch (Exception error)
                    {
                        Debug.LogWarning("[Kimchily Network] Invalid response: " + error.Message);
                        Disconnect("");
                        Status("서버 응답 형식을 확인해 주세요.");
                    }
                }
            }

            if (View.joined && LocalPlayer != null && LocalPlayer.isActiveAndEnabled && Time.unscaledTime >= nextPose)
            {
                nextPose = Time.unscaledTime + .1f;
                var p = LocalPlayer.transform.position;
                Send(new ChatCommand
                {
                    type = "state",
                    state = new ChatPose
                    {
                        sequence = ++sequence,
                        x = p.x,
                        y = p.y,
                        z = p.z,
                        yaw = LocalPlayer.transform.eulerAngles.y,
                        speed = Mathf.Clamp(LocalPlayer.PlanarSpeed, 0, 25),
                        grounded = LocalPlayer.IsGrounded,
                        verticalVelocity = Mathf.Clamp(LocalPlayer.VerticalVelocity, -100, 100)
                    }
                });
            }
        }

        void Handle(ChatEvent message)
        {
            if (message == null || message.protocolVersion != 1)
            {
                throw new InvalidOperationException();
            }

            switch (message.type)
            {
                case "hello":
                    View.selfId = message.selfId;
                    break;
                case "joined":
                    if (message.room == null
                        || message.room.worldId != View.worldId
                        || message.room.revisionId != View.revisionId
                        || message.room.roomId != View.settings.roomId)
                    {
                        throw new InvalidOperationException();
                    }

                    View.selfId = message.selfId;
                    View.joined = true;
                    View.connecting = false;
                    View.status = "연결됨 · " + View.settings.name;
                    players.Clear();
                    if (message.players != null)
                    {
                        players.AddRange(message.players);
                    }

                    lines.Clear();
                    if (message.history != null)
                    {
                        lines.AddRange(message.history);
                    }

                    Debug.Log("[Kimchily Network] Joined " + View.worldId + "/" + View.revisionId
                        + " as " + View.settings.name + " players=" + players.Count);
                    break;
                case "playerJoined":
                    if (message.player != null && !players.Exists(p => p.playerId == message.player.playerId))
                    {
                        players.Add(message.player);
                    }

                    break;
                case "playerLeft":
                    if (message.player != null)
                    {
                        players.RemoveAll(p => p.playerId == message.player.playerId);
                    }

                    break;
                case "state":
                    if (message.player != null)
                    {
                        int index = players.FindIndex(p => p.playerId == message.player.playerId);
                        if (index >= 0)
                        {
                            players[index] = message.player;
                        }
                    }

                    View.players = players.ToArray();
                    ServerEvent?.Invoke(message);
                    return;
                case "game":
                    ServerEvent?.Invoke(message);
                    return;
                case "chat":
                    if (message.chat != null)
                    {
                        lines.Add(message.chat);
                        if (lines.Count > 100)
                        {
                            lines.RemoveAt(0);
                        }
                    }

                    break;
                case "error":
                    if (!View.joined)
                    {
                        Disconnect("");
                    }

                    View.status = message.message;
                    break;
            }

            Render();
            ServerEvent?.Invoke(message);
        }

        void Status(string text)
        {
            View.status = text;
            Render();
        }

        void Render()
        {
            View.players = players.ToArray();
            View.messages = lines.ToArray();
            Changed?.Invoke();
        }

        void OnDisable()
        {
            if (Instance == this)
            {
                View.expanded = false;
                ApplyInputPause();
                Disconnect("");
            }
        }

        void OnDestroy()
        {
            if (Instance != this)
            {
                return;
            }

            View.expanded = false;
            ApplyInputPause();
            Disconnect("");
#if UNITY_WEBGL && !UNITY_EDITOR
            KimchilyChat_Destroy();
#endif
            Instance = null;
        }
    }
}
