using System;
using UnityEngine;
using UnityEngine.Scripting;

namespace Kimchily.Networking
{
    [Serializable, Preserve] public sealed class PortalPadState
    {
        public string id, playerId;
        public float x, y, z, radius;
        public bool active;
    }

    [Serializable, Preserve] public sealed class PortalGameState
    {
        public string preset, phase;
        public int round, requiredPlayers, holdSeconds, remainingMs;
        public long version;
        public PortalPadState[] pads = Array.Empty<PortalPadState>();
    }

    [Serializable] sealed class RoomSnapshot
    {
        public bool connected;
        public string selfId;
        public ChatPlayer[] players;
    }

    /// <summary>Portable facade used by Creator scripts. Commands are requests; only the server decides game state.</summary>
    [Preserve] public static class CoopPortalApi
    {
        public const string Preset = "chili-portal-v1";
        public static event Action Changed;
        static int snapshotFrame = -1;
        static string snapshot;
        [Preserve] public static bool EnableGame(string preset)
        {
            if (preset != Preset) return false;
            return InGameChat.Ensure().Portal.EnableGame(preset);
        }
        [Preserve] public static bool StartRound() => InGameChat.Instance != null && InGameChat.Instance.Portal.StartRound();
        [Preserve] public static bool Replay() => InGameChat.Instance != null && InGameChat.Instance.Portal.Replay();
        [Preserve] public static string GetStateJson()
        {
            if (snapshotFrame == Time.frameCount && snapshot != null) return snapshot;
            var chat = InGameChat.Instance;
            var room = new RoomSnapshot
            {
                connected = chat != null && chat.View.joined,
                selfId = chat != null ? chat.View.selfId : "",
                players = chat != null ? chat.View.players : Array.Empty<ChatPlayer>()
            };
            // Build an explicit null instead of relying on Unity's nested-object null serialization.
            string json = JsonUtility.ToJson(room);
            var game = chat != null && chat.Portal.GameEnabled ? chat.Portal.State : null;
            snapshotFrame = Time.frameCount;
            snapshot = json.Substring(0, json.Length - 1) + ",\"game\":" + (game != null ? JsonUtility.ToJson(game) : "null") + "}";
            return snapshot;
        }
        internal static void NotifyChanged() { snapshotFrame = -1; Changed?.Invoke(); }
    }

    /// <summary>Room-scoped opt-in, reconnect and late-join state for the Chili Island demo.</summary>
    [Preserve, DisallowMultipleComponent] public sealed class CoopPortalClient : MonoBehaviour
    {
        public bool GameEnabled { get; private set; }
        public PortalGameState State { get; private set; }
        public event Action Changed;
        public InGameChat Chat { get; private set; }
        string worldId, revisionId;
        bool wasJoined;
        float nextRequest;
        PortalGameHud hud;

        public void Initialize(InGameChat chat)
        {
            Chat = chat;
            chat.ServerEvent += OnServerEvent;
            chat.Changed += OnRoomChanged;
        }
        public bool EnableGame(string preset)
        {
            if (preset != CoopPortalApi.Preset) return false;
            if (GameEnabled) return true;
            GameEnabled = true;
            if (hud == null) { hud = gameObject.AddComponent<PortalGameHud>(); hud.Initialize(this); }
            if (Chat.View.joined) Chat.SendGameCommand(CoopPortalApi.Preset, "watch");
            NotifyChanged();
            return true;
        }
        public void EnterWorld(string nextWorldId, string nextRevisionId)
        {
            // A scene script can opt in before the host finishes EnterWorld. The first bind preserves it.
            if (worldId != null && (worldId != nextWorldId || revisionId != nextRevisionId)) ResetWorld();
            worldId = nextWorldId; revisionId = nextRevisionId;
        }
        public void ResetWorld()
        {
            GameEnabled = false; State = null; worldId = revisionId = null;
            wasJoined = false; nextRequest = 0;
            NotifyChanged();
        }
        public bool StartRound() => Request("start", State != null && State.phase == "waiting");
        public bool Replay() => Request("reset", State != null && State.phase != "waiting");
        bool Request(string action, bool allowed)
        {
            if (!GameEnabled || !allowed || Time.unscaledTime < nextRequest || !Chat.SendGameCommand(CoopPortalApi.Preset, action)) return false;
            nextRequest = Time.unscaledTime + .6f;
            return true;
        }
        void OnRoomChanged()
        {
            if (!Chat.View.joined)
            {
                if (wasJoined) { State = null; nextRequest = 0; }
                wasJoined = false;
            }
            else wasJoined = true;
            NotifyChanged();
        }
        void OnServerEvent(ChatEvent message)
        {
            if (!GameEnabled) return;
            if (message.type == "joined")
            {
                State = null;
                AcceptState(message.game);
                Chat.SendGameCommand(CoopPortalApi.Preset, "watch");
            }
            else if (message.type == "game") AcceptState(message.game);
        }
        internal bool AcceptState(PortalGameState state)
        {
            if (!IsValidState(state) || (State != null && state.version <= State.version)) return false;
            State = state;
            NotifyChanged();
            return true;
        }
        public static bool IsValidState(PortalGameState state)
        {
            if (state == null || state.preset != CoopPortalApi.Preset || state.version < 0 || state.round < 0
                || state.requiredPlayers < 1 || state.requiredPlayers > 4 || state.holdSeconds != 3
                || state.remainingMs < 0 || state.remainingMs > state.holdSeconds * 1000 || state.pads == null || state.pads.Length != 4) return false;
            if (state.phase != "waiting" && state.phase != "playing" && state.phase != "holding" && state.phase != "complete") return false;
            for (int i = 0; i < state.pads.Length; i++)
            {
                var pad = state.pads[i];
                if (pad == null || !ChatValidation.IsId(pad.id) || !Finite(pad.x) || !Finite(pad.y) || !Finite(pad.z)
                    || !Finite(pad.radius) || pad.radius <= 0 || pad.radius > 10) return false;
                for (int j = 0; j < i; j++) if (state.pads[j].id == pad.id) return false;
            }
            return true;
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        void NotifyChanged() { Changed?.Invoke(); CoopPortalApi.NotifyChanged(); }
        void OnDestroy()
        {
            if (Chat != null) { Chat.ServerEvent -= OnServerEvent; Chat.Changed -= OnRoomChanged; }
        }
    }
}
