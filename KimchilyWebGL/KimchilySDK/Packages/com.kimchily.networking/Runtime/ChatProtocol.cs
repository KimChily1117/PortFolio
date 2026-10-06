using System;

namespace Kimchily.Networking
{
    [Serializable]
    public sealed class ChatSettings
    {
        public string endpoint = "ws://127.0.0.1:8790/ws";
        public string name = "Guest";
        public string roomId = "playground";
    }

    [Serializable]
    public sealed class ChatCommand
    {
        public int protocolVersion = 1;
        public string type;
        public string worldId;
        public string revisionId;
        public string roomId;
        public string name;
        public string text;
        public string scriptId;
        public string scriptHash;
        public string action;
        public string payloadJson;
        public ChatPose state;
    }

    [Serializable]
    public sealed class ChatPose
    {
        public long sequence;
        public float x;
        public float y;
        public float z;
        public float yaw;
        public float speed;
        public float verticalVelocity;
        public bool grounded;
    }

    [Serializable]
    public sealed class ChatPlayer
    {
        public string playerId;
        public string name;
        public ChatPose state;
    }

    [Serializable]
    public sealed class ChatLine
    {
        public string id;
        public string playerId;
        public string name;
        public string text;
        public string sentAtUtc;
    }

    [Serializable]
    public sealed class ChatRoom
    {
        public string worldId;
        public string revisionId;
        public string roomId;
    }

    [Serializable]
    public sealed class ChatEvent
    {
        public int protocolVersion;
        public string type;
        public string selfId;
        public string code;
        public string message;
        public ChatRoom room;
        public ChatPlayer player;
        public ChatPlayer[] players;
        public ChatLine chat;
        public ChatLine[] history;
        public ScriptGameState game;
    }

    [Serializable]
    public sealed class ChatView
    {
        public string worldId;
        public string revisionId;
        public string status = "서버에 연결해 함께 대화하세요.";
        public string selfId;
        public bool expanded;
        public bool connecting;
        public bool joined;
        public ChatSettings settings = new ChatSettings();
        public ChatPlayer[] players = Array.Empty<ChatPlayer>();
        public ChatLine[] messages = Array.Empty<ChatLine>();
    }

    [Serializable]
    public sealed class ChatWireEvent
    {
        public int generation;
        public string type;
        public string data;
    }

    public static class ChatValidation
    {
        public static bool IsId(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 80)
            {
                return false;
            }

            foreach (char c in value)
            {
                if (!(c >= 'a' && c <= 'z') && !(c >= 'A' && c <= 'Z') && !(c >= '0' && c <= '9') && c != '-' && c != '_')
                {
                    return false;
                }
            }

            return true;
        }

        public static bool IsEndpoint(string value)
        {
            Uri uri;
            return Uri.TryCreate(value, UriKind.Absolute, out uri)
                && (uri.Scheme == "ws" || uri.Scheme == "wss")
                && string.IsNullOrEmpty(uri.UserInfo)
                && uri.AbsolutePath == "/ws"
                && string.IsNullOrEmpty(uri.Query)
                && string.IsNullOrEmpty(uri.Fragment);
        }

        public static bool IsText(string value, int max)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > max)
            {
                return false;
            }

            foreach (char c in value)
            {
                if (char.IsControl(c))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
