namespace Kimchily.Server.Core;

/// <summary>
/// Fixed, server-owned cooperative rules for the authored Chili Island scene.
/// Positions are client simulated and sanity checked by RoomHub; this is not authoritative physics.
/// </summary>
internal sealed class PortalPuzzle
{
    public const string Preset = "chili-portal-v1";
    private const int HoldMilliseconds = 3000;
    private const int PoseFreshMilliseconds = 1200;
    private static readonly PortalPad[] Layout =
    [
        new("star", -3, 0, 2, 1.1f, false, null),
        new("moon", 3, 0, 2, 1.1f, false, null),
        new("sun", -3, 0, 6, 1.1f, false, null),
        new("leaf", 3, 0, 6, 1.1f, false, null)
    ];
    private long? _holdingSince;
    public PortalGameState State { get; private set; } =
        new(Preset, "waiting", 0, 1, HoldMilliseconds / 1000, HoldMilliseconds, Layout, 0);

    public bool Start(int memberCount)
    {
        if (State.Phase != "waiting") return false;
        State = State with
        {
            Phase = "playing", Round = State.Round + 1, RequiredPlayers = Math.Clamp(memberCount, 1, Layout.Length),
            RemainingMs = HoldMilliseconds, Version = State.Version + 1
        };
        _holdingSince = null;
        return true;
    }

    public bool Reset()
    {
        if (State.Phase == "waiting") return false;
        State = State with { Phase = "waiting", RemainingMs = HoldMilliseconds, Version = State.Version + 1 };
        _holdingSince = null;
        return true;
    }

    public bool Update(IEnumerable<(PlayerInfo Player, long ReceivedAt)> players, long now)
    {
        // Keep the victory board lit, including for late joiners, until an explicit reset.
        if (State.Phase == "complete") return false;
        var participants = players.OrderBy(item => item.Player.PlayerId, StringComparer.Ordinal).ToArray();
        var required = State.Phase == "waiting" ? Math.Clamp(participants.Length, 1, Layout.Length) : State.RequiredPlayers;
        var used = new HashSet<string>(StringComparer.Ordinal);
        var pads = Layout.Select((pad, index) =>
        {
            var active = index < required;
            string? occupant = null;
            if (active)
            {
                foreach (var participant in participants)
                {
                    if (participant.Player.State is not { Grounded: true } pose || used.Contains(participant.Player.PlayerId)
                        || now - participant.ReceivedAt > PoseFreshMilliseconds || now < participant.ReceivedAt) continue;
                    var dx = pose.X - pad.X;
                    var dz = pose.Z - pad.Z;
                    if (dx * dx + dz * dz > pad.Radius * pad.Radius || Math.Abs(pose.Y - pad.Y) > 1.5f) continue;
                    occupant = participant.Player.PlayerId;
                    used.Add(occupant);
                    break;
                }
            }
            return pad with { Active = active, PlayerId = occupant };
        }).ToArray();
        var phase = State.Phase;
        var remaining = HoldMilliseconds;
        if (phase != "waiting")
        {
            if (pads.Count(pad => pad.Active && pad.PlayerId is not null) == required)
            {
                _holdingSince ??= now;
                var milliseconds = Math.Max(0, HoldMilliseconds - (now - _holdingSince.Value));
                // Quantize presentation to 100 ms so independent player samples do not flood snapshots.
                remaining = (int)((milliseconds + 99) / 100 * 100);
                phase = milliseconds == 0 ? "complete" : "holding";
            }
            else
            {
                _holdingSince = null;
                phase = "playing";
            }
        }
        if (State.Version != 0 && State.Phase == phase && State.RequiredPlayers == required
            && State.RemainingMs == remaining && State.Pads.SequenceEqual(pads)) return false;
        State = State with
        {
            Phase = phase, RequiredPlayers = required, RemainingMs = remaining, Pads = pads, Version = State.Version + 1
        };
        return true;
    }
}
