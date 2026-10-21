using System;
using Google.Protobuf.Protocol;
using ProjectDawn.Combat;
using Server.Game.Room;

namespace Server
{
    public partial class ClientSession
    {
        public bool RaidClockSync { get; private set; }
        public int RaidProtocolVersion { get; private set; }
        // Immutable route published only after every binding has been installed on the room thread.
        internal volatile RaidIngress RaidRoute;
        internal bool RaidMatchRequested;

        public void HandleRaidHello(C_RaidHello hello)
        {
            if (RaidMatchRequested || IsTransferring || MyPlayer?.Room?.UsesRaidCore == true)
            { Send(new S_RaidHello { Version = RaidProtocolVersion, Reason = "NegotiationLocked" }); return; }
            RaidProtocolVersion = hello.Version == 1 || hello.Version == 3 ? hello.Version : 0;
            RaidClockSync = hello.ClockSync;
            Send(new S_RaidHello { Version = RaidProtocolVersion, Reason = RaidProtocolVersion > 0 ? "Ready" : "UnsupportedVersion" });
        }
        public void HandleRaidClockResponse(C_RaidClockResponse packet)
        {
            var route=RaidRoute;
            if(route==null || IsTransferring || MyPlayer?.Room!=route.Room) return;
            if(!route.Allow()) {Disconnect();return;}
            route.ClockResponse(packet, this);
        }
        public void HandleRaidInput(C_RaidInput packet)
        {
            var route = RaidRoute;
            if (route == null || IsTransferring || MyPlayer?.Room != route.Room || packet.Key == null) return;
            if (!route.Allow()) { Disconnect(); return; }
            var key = packet.Key;
            route.Combat.ReceiveInput(route.Binding, new PlayerInput(
                new InputKey(key.Encounter, key.Player, key.Epoch, key.Sequence),
                packet.ClientTimeUs, packet.ClockVersion, (InputKind)packet.Kind, packet.Value, packet.MoveX, packet.MoveY, packet.Buttons));
        }
        public void HandleRaidProposal(C_RaidProposal packet)
        {
            if(packet.Program.Length>512){Disconnect();return;}
            var route = RaidRoute;
            if (route == null || IsTransferring || MyPlayer?.Room != route.Room) return;
            if (!route.Allow()) { Disconnect(); return; }
            InputKey? origin = packet.Origin == null ? (InputKey?)null : new InputKey(packet.Origin.Encounter,
                packet.Origin.Player, packet.Origin.Epoch, packet.Origin.Sequence);
            route.Combat.ReceiveHostProposal(route.Binding, new HostProposal(packet.Encounter, packet.HostEpoch,
                packet.Sequence, packet.StartTick, packet.Actor, packet.Definition, origin, packet.Program));
        }
    }

    internal sealed class RaidIngress
    {
        public readonly GameRoom Room;
        public readonly Server.Game.RaidCombatSession Combat;
        public readonly SessionBinding Binding;
        private long _window = Environment.TickCount64;
        private int _count;
        private long _lastActivityUs;
        public long LastActivityUs=>System.Threading.Interlocked.Read(ref _lastActivityUs);
        private readonly ClockSynchronizer clock;
        public RaidIngress(GameRoom room, Server.Game.RaidCombatSession combat, SessionBinding binding, bool clockSync=false)
        { Room = room; Combat = combat; Binding = binding; _lastActivityUs=combat.NowUs; if(clockSync) clock=new ClockSynchronizer(combat.InputGraceUs); }
        public void PollClock(ClientSession session)
        {
            if(clock==null) return;
            lock(clock) {
                var token=clock.Challenge(Combat.NowUs);
                if(token!=null) session.Send(new S_RaidClockChallenge {Encounter=Combat.Engine.EncounterId, Epoch=Binding.Epoch, Token=token});
            }
        }
        public void ClockResponse(C_RaidClockResponse packet, ClientSession session)
        {
            if(clock==null || packet.Encounter!=Combat.Engine.EncounterId || packet.Epoch!=Binding.Epoch) return;
            lock(clock) {
                var mapping=clock.Respond(packet.Token, packet.ClientReceivedUs, packet.ClientSentUs, Combat.NowUs);
                if(mapping==null) return;
                if(!mapping.Trusted)Console.WriteLine($"[RAID_CLOCK_FALLBACK] encounter={packet.Encounter} player={Binding.Player} version={mapping.Version} measuredRttUs={clock.RoundTripUs} rejectedSamples={clock.RejectedSamples}");
                Binding.InstallClock(mapping);
                session.Send(new S_RaidClockState {Encounter=packet.Encounter, Version=mapping.Version, OffsetUs=mapping.OffsetUs,
                    Trusted=mapping.Trusted, ExpiresUs=mapping.ExpiresUs, MaxBackdateUs=mapping.MaxBackdateUs,
                    UncertaintyUs=mapping.UncertaintyUs, RoundTripUs=clock.RoundTripUs});
            }
        }
        public bool Allow()
        {
            lock (this)
            {
                long now = Environment.TickCount64;
                if (now - _window >= 1000) { _window = now; _count = 0; }
                System.Threading.Interlocked.Exchange(ref _lastActivityUs,Combat.NowUs);
                return ++_count <= 240; // Per authenticated connection, including host proposals.
            }
        }
    }
}
