using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kimchily.Server.Core;

public static class Protocol
{
    public const int Version = 1;
    public const int MaxMessageBytes = 4096;
    public static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 8
    };
}

public sealed record ClientCommand
{
    public int ProtocolVersion { get; init; }
    public string? Type { get; init; }
    public string? WorldId { get; init; }
    public string? RevisionId { get; init; }
    public string? RoomId { get; init; }
    public string? Name { get; init; }
    public string? Text { get; init; }
    public PlayerState? State { get; init; }
    public string? Preset { get; init; }
    public string? Action { get; init; }
}

public sealed record RoomKey(string WorldId, string RevisionId, string RoomId);
public sealed record PlayerState(long Sequence, float X, float Y, float Z, float Yaw, float Speed, bool Grounded, float VerticalVelocity);
public sealed record PlayerInfo(string PlayerId, string Name, PlayerState? State = null);
public sealed record ChatMessage(string Id, string PlayerId, string Name, string Text, DateTimeOffset SentAtUtc);
public sealed record RoomSummary(RoomKey Room, int PlayerCount, int Capacity);
public sealed record PortalPad(string Id, float X, float Y, float Z, float Radius, bool Active, string? PlayerId);
public sealed record PortalGameState(string Preset, string Phase, int Round, int RequiredPlayers,
    int HoldSeconds, int RemainingMs, PortalPad[] Pads, long Version);

public sealed record ServerEvent(string Type)
{
    public int ProtocolVersion { get; init; } = Protocol.Version;
    public string? SelfId { get; init; }
    public RoomKey? Room { get; init; }
    public PlayerInfo[]? Players { get; init; }
    public PlayerInfo? Player { get; init; }
    public ChatMessage? Chat { get; init; }
    public ChatMessage[]? History { get; init; }
    public PortalGameState? Game { get; init; }
    public string? Code { get; init; }
    public string? Message { get; init; }
}

public interface IRoomPeer
{
    string Id { get; }
    // Must enqueue without blocking. A full outgoing queue disconnects the slow peer.
    bool Send(ServerEvent message);
}
