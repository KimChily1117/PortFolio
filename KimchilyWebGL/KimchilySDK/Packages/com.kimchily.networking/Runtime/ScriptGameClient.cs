using System;
using System.Text;
using UnityEngine;
using UnityEngine.Scripting;

namespace Kimchily.Networking
{
    /// <summary>
    /// C#이 이해하는 것은 스크립트 신원과 전송 순서뿐이다.
    /// stateJson 안의 필드, 승리 조건, 시간 계산은 서버와 제작 씬의 TypeScript가 소유한다.
    /// </summary>
    [Serializable, Preserve]
    public sealed class ScriptGameState
    {
        public string scriptId;
        public string scriptHash;
        public string stateJson;
        public long version;
    }

    [Serializable]
    sealed class RoomSnapshot
    {
        public bool connected;
        public string selfId;
        public ChatPlayer[] players;
    }

    /// <summary>
    /// TS VM과 Unity 소켓 사이의 작은 API 표면. JSON은 복사본이며 CLR 객체를 TS에 노출하지 않는다.
    /// 조회는 HUD나 연결을 만들지 않는다. useGame만 방 단위 구독 의도를 등록한다.
    /// </summary>
    [Preserve]
    public static class ScriptRoomApi
    {
        public static event Action Changed;
        static int snapshotFrame = -1;
        static string snapshot;
        [Preserve]
        public static bool UseGame(string scriptId, string scriptHash)
        {
            if (!ScriptGameClient.IsValidIdentity(scriptId, scriptHash))
            {
                return false;
            }

            return InGameChat.Ensure().Game.UseGame(scriptId, scriptHash);
        }

        [Preserve]
        public static bool SendAction(string action, string payloadJson)
        {
            return InGameChat.Instance != null && InGameChat.Instance.Game.SendAction(action, payloadJson);
        }

        [Preserve]
        public static string GetStateJson()
        {
            if (snapshotFrame == Time.frameCount && snapshot != null)
            {
                return snapshot;
            }

            var chat = InGameChat.Instance;
            var room = new RoomSnapshot
            {
                connected = chat != null && chat.View.joined,
                selfId = chat != null ? chat.View.selfId : "",
                players = chat != null ? chat.View.players : Array.Empty<ChatPlayer>()
            };
            string json = JsonUtility.ToJson(room);
            var game = chat != null && chat.Game.GameEnabled ? chat.Game.State : null;
            // JsonUtility의 중첩 객체 초기화 규칙에 기대지 않고 미수신 상태를 명시적인 null로 보낸다.
            snapshot = json.Substring(0, json.Length - 1) + ",\"game\":" + (game != null ? JsonUtility.ToJson(game) : "null") + "}";
            snapshotFrame = Time.frameCount;
            return snapshot;
        }

        internal static void NotifyChanged()
        {
            snapshotFrame = -1;
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// 게임 규칙을 모르는 방 구독기. 재접속, 월드 수명, SHA-256 신원, 단조 증가 버전만 검증한다.
    /// 연결이 끊기면 서버 상태를 버리고 재접속의 joined/watch 응답으로 다시 시작한다.
    /// </summary>
    [Preserve, DisallowMultipleComponent]
    public sealed class ScriptGameClient : MonoBehaviour
    {
        public const int MaximumPayloadCharacters = 1024;
        public const int MaximumStateCharacters = 16384;
        public bool GameEnabled { get; private set; }
        public string ScriptId { get; private set; } = "";
        public string ScriptHash { get; private set; } = "";
        public ScriptGameState State { get; private set; }

        public event Action Changed;
        public InGameChat Chat { get; private set; }

        string worldId;
        string revisionId;
        bool wasJoined;
        float nextRequest;
        public void Initialize(InGameChat chat)
        {
            Chat = chat;
            chat.ServerEvent += OnServerEvent;
            chat.Changed += OnRoomChanged;
        }

        public bool UseGame(string scriptId, string scriptHash)
        {
            if (!IsValidIdentity(scriptId, scriptHash))
            {
                return false;
            }

            // 한 월드에 여러 스크립트가 있더라도 먼저 선택한 게임 신원을 조용히 바꾸지 않는다.
            // 다른 버전으로 이동하려면 호스트의 정상 퇴장/입장 절차를 거쳐야 한다.
            if (GameEnabled)
            {
                return ScriptId == scriptId && ScriptHash == scriptHash;
            }

            ScriptId = scriptId;
            ScriptHash = scriptHash;
            GameEnabled = true;
            RequestWatch();
            NotifyChanged();
            return true;
        }

        public void EnterWorld(string nextWorldId, string nextRevisionId)
        {
            // 월드 TS의 Start가 호스트 EnterWorld보다 먼저 실행된다. 첫 bind는 대기 구독을 보존한다.
            if (worldId != null && (worldId != nextWorldId || revisionId != nextRevisionId))
            {
                ResetWorld();
            }

            worldId = nextWorldId;
            revisionId = nextRevisionId;
        }

        public void ResetWorld()
        {
            GameEnabled = false;
            State = null;
            ScriptId = ScriptHash = "";
            worldId = revisionId = null;
            wasJoined = false;
            nextRequest = 0;
            NotifyChanged();
        }

        public bool SendAction(string action, string payloadJson)
        {
            payloadJson = payloadJson ?? "null";
            if (!GameEnabled
                || !ChatValidation.IsId(action)
                || string.IsNullOrWhiteSpace(payloadJson)
                || payloadJson.Length > MaximumPayloadCharacters
                || Encoding.UTF8.GetByteCount(payloadJson) > MaximumPayloadCharacters
                || Time.unscaledTime < nextRequest)
            {
                return false;
            }

            if (!Chat.SendGameCommand(ScriptId, ScriptHash, action, payloadJson))
            {
                return false;
            }

            // 범용 입력 폭주 완화다. 이 입력이 현재 게임에서 합법인지 판단하는 곳은 서버 TS다.
            nextRequest = Time.unscaledTime + .6f;
            return true;
        }

        void RequestWatch()
        {
            if (GameEnabled && Chat.View.joined)
            {
                Chat.SendGameCommand(ScriptId, ScriptHash, "watch", "null");
            }
        }

        void OnRoomChanged()
        {
            if (!Chat.View.joined)
            {
                if (wasJoined)
                {
                    State = null;
                    nextRequest = 0;
                }

                wasJoined = false;
            }
            else
            {
                wasJoined = true;
            }

            NotifyChanged();
        }

        internal void OnServerEvent(ChatEvent message)
        {
            if (!GameEnabled)
            {
                return;
            }

            if (message.type == "joined")
            {
                // 새 연결은 이전 소켓의 version보다 낮아도 정상이다. 연결 세대 필터는 InGameChat이 담당한다.
                State = null;
                if (!AcceptState(message.game))
                {
                    NotifyChanged();
                }

                RequestWatch();
            }
            else if (message.type == "game")
            {
                AcceptState(message.game);
            }
        }

        internal bool AcceptState(ScriptGameState state)
        {
            if (!GameEnabled
                || !IsValidState(state)
                || state.scriptId != ScriptId
                || state.scriptHash != ScriptHash
                || (State != null && state.version <= State.version))
            {
                return false;
            }

            State = state;
            NotifyChanged();
            return true;
        }

        public static bool IsValidState(ScriptGameState state)
        {
            return state != null
                && IsValidIdentity(state.scriptId, state.scriptHash)
                && state.version >= 0
                && !string.IsNullOrWhiteSpace(state.stateJson)
                && state.stateJson.Length <= MaximumStateCharacters
                && Encoding.UTF8.GetByteCount(state.stateJson) <= MaximumStateCharacters;
        }

        public static bool IsValidIdentity(string scriptId, string scriptHash)
        {
            if (!ChatValidation.IsId(scriptId) || scriptHash == null || scriptHash.Length != 64)
            {
                return false;
            }

            foreach (char value in scriptHash)
            {
                if (!(value >= '0' && value <= '9') && !(value >= 'a' && value <= 'f'))
                {
                    return false;
                }
            }

            return true;
        }

        void NotifyChanged()
        {
            Changed?.Invoke();
            ScriptRoomApi.NotifyChanged();
        }

        void OnDestroy()
        {
            if (Chat != null)
            {
                Chat.ServerEvent -= OnServerEvent;
                Chat.Changed -= OnRoomChanged;
            }
        }
    }
}
