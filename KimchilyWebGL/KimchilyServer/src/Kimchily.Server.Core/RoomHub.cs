using Kimchily.Server.Core.Jobs;

namespace Kimchily.Server.Core;

/// <summary>
/// Room registry and rules run on one bounded serialized queue for this small demo.
/// No socket, Unity, SQL, character inventory, or Project Dawn RoomType dependency.
/// </summary>
public sealed class RoomHub(Func<long>? clock = null) : JobSerializer(clock: clock)
{
    public const int RoomCapacity = 8;
    public const int MaxRooms = 32;
    private readonly Func<long> _clock = clock ?? (() => Environment.TickCount64);
    private readonly Dictionary<RoomKey, Room> _rooms = [];
    private readonly Dictionary<string, (Room Room, Member Member)> _members = [];

    public Task HandleAsync(IRoomPeer peer, ClientCommand command) => InvokeAsync(() =>
    {
        if (command.ProtocolVersion != Protocol.Version) Error(peer, "PROTOCOL_MISMATCH", "지원하지 않는 통신 버전입니다.");
        else switch (command.Type)
        {
            case "join": Join(peer, command); break;
            case "chat": Chat(peer, command.Text); break;
            case "leave": Leave(peer, acknowledge: true); break;
            case "ping": peer.Send(new ServerEvent("pong")); break;
            default: Error(peer, "UNKNOWN_MESSAGE", "지원하지 않는 메시지입니다."); break;
        }
        return true;
    });

    public Task DisconnectAsync(IRoomPeer peer) => InvokeAsync(() => { Leave(peer, acknowledge: false); return true; });

    public Task<RoomSummary[]> SnapshotAsync() => InvokeAsync(() => _rooms.Values
        .Select(room => new RoomSummary(room.Key, room.Members.Count, RoomCapacity)).ToArray());

    private void Join(IRoomPeer peer, ClientCommand command)
    {
        if (_members.ContainsKey(peer.Id)) { Error(peer, "ALREADY_JOINED", "현재 방에서 나간 뒤 입장해 주세요."); return; }
        if (!ValidId(command.WorldId) || !ValidId(command.RevisionId) || !ValidId(command.RoomId))
        { Error(peer, "INVALID_ROOM", "월드·버전·방 코드는 영문, 숫자, 밑줄, 하이픈 1~64자입니다."); return; }
        var name = command.Name?.Trim();
        if (!ValidText(name, 24)) { Error(peer, "INVALID_NAME", "닉네임을 1~24자로 입력해 주세요."); return; }
        var key = new RoomKey(command.WorldId!, command.RevisionId!, command.RoomId!);
        if (!_rooms.TryGetValue(key, out var room))
        {
            if (_rooms.Count >= MaxRooms) { Error(peer, "SERVER_FULL", "현재 새 방을 만들 수 없습니다."); return; }
            room = new Room(key);
            _rooms.Add(key, room);
        }
        if (room.Members.Count >= RoomCapacity) { Error(peer, "ROOM_FULL", "방 정원 8명이 모두 찼습니다."); return; }
        var member = new Member(peer, new PlayerInfo(peer.Id, name!));
        room.Members.Add(peer.Id, member);
        _members.Add(peer.Id, (room, member));
        peer.Send(new ServerEvent("joined")
        {
            Room = key, SelfId = peer.Id,
            Players = room.Members.Values.Select(value => value.Player).ToArray(),
            History = room.History.ToArray()
        });
        Broadcast(room, new ServerEvent("playerJoined") { Player = member.Player }, except: peer.Id);
    }

    private void Chat(IRoomPeer peer, string? rawText)
    {
        if (!_members.TryGetValue(peer.Id, out var membership))
        { Error(peer, "NOT_JOINED", "먼저 방에 입장해 주세요."); return; }
        var text = rawText?.Trim();
        if (!ValidText(text, 300)) { Error(peer, "INVALID_CHAT", "메시지를 1~300자로 입력해 주세요."); return; }
        var recent = membership.Member.ChatTimes;
        var now = _clock();
        while (recent.TryPeek(out var timestamp) && now - timestamp >= 5000) recent.Dequeue();
        if (recent.Count >= 5) { Error(peer, "CHAT_RATE_LIMIT", "메시지가 너무 빠릅니다. 잠시 후 보내 주세요."); return; }
        recent.Enqueue(now);
        var message = new ChatMessage(Guid.NewGuid().ToString("N"), peer.Id, membership.Member.Player.Name, text!, DateTimeOffset.UtcNow);
        membership.Room.History.Enqueue(message);
        while (membership.Room.History.Count > 20) membership.Room.History.Dequeue();
        Broadcast(membership.Room, new ServerEvent("chat") { Chat = message });
    }

    private void Leave(IRoomPeer peer, bool acknowledge)
    {
        if (_members.Remove(peer.Id, out var membership))
        {
            membership.Room.Members.Remove(peer.Id);
            Broadcast(membership.Room, new ServerEvent("playerLeft") { Player = membership.Member.Player });
            if (membership.Room.Members.Count == 0) _rooms.Remove(membership.Room.Key);
        }
        if (acknowledge) peer.Send(new ServerEvent("left"));
    }

    private static void Broadcast(Room room, ServerEvent message, string? except = null)
    {
        foreach (var member in room.Members.Values)
            if (member.Peer.Id != except) member.Peer.Send(message);
    }

    private static bool ValidId(string? value) => value is { Length: >= 1 and <= 64 }
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-');

    private static bool ValidText(string? value, int maximum) => !string.IsNullOrWhiteSpace(value)
        && value.Length <= maximum && !value.Any(char.IsControl);

    private static void Error(IRoomPeer peer, string code, string message) => peer.Send(new ServerEvent("error") { Code = code, Message = message });

    private sealed class Room(RoomKey key)
    {
        public RoomKey Key { get; } = key;
        public Dictionary<string, Member> Members { get; } = [];
        public Queue<ChatMessage> History { get; } = [];
    }

    private sealed class Member(IRoomPeer peer, PlayerInfo player)
    {
        public IRoomPeer Peer { get; } = peer;
        public PlayerInfo Player { get; } = player;
        public Queue<long> ChatTimes { get; } = [];
    }
}
