using System;
using System.Diagnostics;
using ProjectDawn.Combat;

namespace Server.Game
{
    // Adapter owned by a negotiated four-player GameRoom.
    // Construct/bind/pump/drain on the owning GameRoom thread; receive directly at authenticated ingress
    // so room-queue delay does not change the server receipt timestamp.
    public sealed class RaidCombatSession
    {
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        public long InputGraceUs {get;}
        public EncounterEngine Engine { get; }
        public long NowUs => checked((long)(_clock.ElapsedTicks * (1000000.0 / Stopwatch.Frequency)));

        public RaidCombatSession(long encounterId, int hostPlayer, long hostEpoch,
            TimingPolicy timing, CombatRules rules, CombatState initial)
        {
            InputGraceUs=timing.InputGraceUs;
            Engine = new EncounterEngine(encounterId, hostPlayer, hostEpoch, timing, rules, initial,
                () => NowUs);
        }

        public void BindAuthenticatedParticipant(SessionBinding binding) => Engine.Bind(binding);
        public bool ReceiveInput(SessionBinding binding, PlayerInput input) => Engine.ReceiveInput(binding, input);
        public bool ReceiveHostProposal(SessionBinding binding, HostProposal proposal) => Engine.ReceiveProposal(binding, proposal);
        public void UpdateOnRoomThread() => Engine.Pump();
        public CombatEvent[] DrainFinalEventsOnRoomThread() => Engine.DrainCommittedEvents();
        public void AbortOnRoomThread(string reason) => Engine.Abort(reason);
    }
}
