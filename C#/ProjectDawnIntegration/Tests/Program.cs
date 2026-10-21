using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ProjectDawn.Combat;

internal static class Program
{
    private sealed class Fixture
    {
        public long Now;
        public readonly SessionBinding[] Sessions = Enumerable.Range(1, 4).Select(i => new SessionBinding(i, 1, 1, 0, true)).ToArray();
        public readonly EncounterEngine Engine;
        public readonly TimingPolicy Policy;
        public Fixture(int pending = 4096, int bossX = 0)
        {
            // Synthetic test timing only. Production L/P/W are not selected by this fixture.
            Policy = new TimingPolicy(10000, 40000, 20000, 70000, 1000, maxPending: pending);
            var rules = new CombatRules(new[] {
                new AttackDefinition(1, false, false, 25, 500, delayTicks: 1, cooldownTicks: 1),
                new AttackDefinition(2, true, true, 20, 2000, delayTicks: 1, stunTicks: 3, knockback: 700),
                new AttackDefinition(3, true, false, 20, 2000, delayTicks: 1),
                new AttackDefinition(4, false, false, 10, 500, delayTicks: 1, repeat: 3, intervalTicks: 1),
            });
            Engine = new EncounterEngine(77, 1, 1, Policy, rules, CombatState.FourPlayers(bossX), () => Now);
            foreach (var s in Sessions) Engine.Bind(s);
        }
        public InputKey Key(int player, long sequence, long epoch = 1) => new InputKey(77, player, epoch, sequence);
        public PlayerInput Input(int player, long sequence, long tick, InputKind kind, int value = 0) =>
            new PlayerInput(Key(player, sequence), (tick - 1) * 10000, 1, kind, value);
        public void Submit(PlayerInput input) => Engine.ReceiveInput(Sessions[input.Key.Player - 1], input);
        public void Boss(int def = 2, long seq = 1, long start = 3) => Engine.ReceiveProposal(Sessions[0], new HostProposal(77, 1, seq, start, 100, def));
        public void Attack(int player, long sequence, long tick, int def = 1, long proposal = 2)
        {
            var input = Input(player, sequence, tick, InputKind.Attack, def);
            Submit(input);
            Engine.ReceiveProposal(Sessions[0], new HostProposal(77, 1, proposal, tick, player, def, input.Key));
        }
        public void Pump(long now) { Now = now; Engine.Pump(); Check(!Engine.IsAborted, Engine.AbortReason ?? "unexpected abort"); }
    }

    private static readonly List<string> Passed = new List<string>();
    private static int Main()
    {
        try
        {
            Run("four players + finalized snapshot isolation", () => {
                var f = new Fixture(); Check(f.Engine.FinalSnapshot.Fighters.Count == 5, "roster");
                var copy = f.Engine.FinalSnapshot; copy.Fighters[1].Hp = 0;
                Check(f.Engine.FinalSnapshot.Fighters[1].Hp == 100, "snapshot leaked");
            });
            Run("late jump removes hit, knockback and dependent attack rejection", () => {
                var f = Scenario(false); var early = Scenario(true);
                Check(f.Engine.PreviewSnapshot.Fighters[1].Hp == 80, "expected provisional damage");
                Check(f.Engine.PreviewSnapshot.Fighters[1].X == 700, "expected provisional knockback");
                Check(f.Engine.FinalSnapshot.Fighters[1].Hp == 100, "provisional damage leaked");
                f.Now = 65000; f.Submit(f.Input(1, 1, 3, InputKind.Jump)); f.Pump(65000);
                Check(f.Engine.PreviewSnapshot.Fighters[1].Hp == 100 && f.Engine.PreviewSnapshot.Fighters[1].X == 0, "jump did not erase dependency chain");
                Check(f.Engine.PreviewSnapshot.Fighters[100].Hp == 975, "follow-up attack not restored");
                Compare(f.Engine.PreviewSnapshot, early.Engine.PreviewSnapshot);
                f.Pump(140000); early.Pump(140000);
                Compare(f.Engine.FinalSnapshot, early.Engine.FinalSnapshot);
                Check(f.Engine.FinalSnapshot.Fighters[2].Hp == 80, "other players not evaluated");
                Check(f.Engine.DrainCommittedEvents().Count(x => x.Kind == "Evaded" && x.Target == 1) == 1, "evade event count");
            });
            Run("strict input deadline before / equal / after", () => {
                foreach (long receive in new[] { 69999L, 70000L, 70001L }) {
                    var f = new Fixture(); f.Now = receive; f.Submit(f.Input(1, 1, 3, InputKind.Jump)); f.Pump(receive);
                    Check(f.Engine.DrainInputReceipts().Single().Result == (receive < 70000 ? Admission.Admitted : Admission.TooLate), "deadline edge");
                }
            });
            Run("ingress receipt survives delayed room queue processing", () => {
                var f = new Fixture(); f.Boss(); f.Now = 69999; f.Submit(f.Input(1, 1, 3, InputKind.Jump)); f.Pump(140000);
                Check(f.Engine.FinalSnapshot.Fighters[1].Hp == 100 && f.Engine.FinalSnapshot.Fighters[2].Hp == 80, "queued input dropped");
            });
            Run("finalized damage cannot be rewritten", () => {
                var f = new Fixture(); f.Boss(); f.Pump(140000);
                long before = f.Engine.FinalizedThroughTick; f.Now = 145000; f.Submit(f.Input(1, 1, 3, InputKind.Jump)); f.Pump(145000);
                Check(f.Engine.DrainInputReceipts().Single().Result == Admission.TooLate, "accepted old jump");
                Check(f.Engine.FinalSnapshot.Fighters[1].Hp == 80 && f.Engine.FinalizedThroughTick >= before, "rewrote finalized result");
            });
            Run("duplicate is idempotent and payload mutation conflicts", () => {
                var f = new Fixture(); var input = f.Input(1, 1, 1, InputKind.Jump); f.Submit(input); f.Pump(10000);
                f.Now = 20000; f.Submit(input); f.Submit(new PlayerInput(input.Key, 10000, 1, InputKind.Jump)); f.Pump(20000);
                var r = f.Engine.DrainInputReceipts();
                Check(r[1].Result == Admission.Duplicate && r[1].ReceivedUs == 0 && r[2].Result == Admission.Conflict, "retransmission changed identity");
                f.Pump(100000); Check(f.Engine.DrainCommittedEvents().Count(x => x.Origin == null ? false : x.Origin.Value.Equals(input.Key)) == 1, "duplicate effects");
            });
            Run("four-player multi-hit and repeat replay produce single outcomes", () => {
                var f = new Fixture(); for (int id = 1; id <= 4; id++) f.Attack(id, 1, 1, 4, id);
                f.Pump(40000); f.Pump(40000); f.Pump(40000);
                Check(f.Engine.DrainCommittedEvents().Length == 0, "premature outcome");
                Check(f.Engine.PreviewSnapshot.Fighters[100].Hp == 880, "multi-hit total");
                f.Pump(120000); var ev = f.Engine.DrainCommittedEvents();
                Check(ev.Count(x => x.Kind == "HitResolved") == 12, "multi-hit suppressed or doubled");
                Check(ev.Select(x => x.Sequence).Distinct().Count() == ev.Length, "event seq duplicated");
                f.Pump(120000); Check(f.Engine.DrainCommittedEvents().Length == 0, "recommit");
            });
            Run("meteor direct hit ignores jump while ground shockwave is dodged", () => {
                var f = new Fixture(); f.Boss(3); f.Now = 20000; f.Submit(f.Input(1, 1, 3, InputKind.Jump)); f.Pump(120000);
                Check(f.Engine.FinalSnapshot.Fighters[1].Hp == 80, "direct meteor became jump-invulnerable");
            });
            Run("spoofed participant does not reserve victim input key", () => {
                var f = new Fixture(); var input = f.Input(2, 1, 1, InputKind.Jump);
                f.Engine.ReceiveInput(f.Sessions[0], input); f.Submit(input); f.Pump(10000);
                var r = f.Engine.DrainInputReceipts(); Check(r[0].Result == Admission.WrongSession && r[1].Result == Admission.Admitted, "spoof occupied key");
            });
            Run("stale session epoch and other encounter rejected", () => {
                var f = new Fixture(); f.Submit(f.Input(1, 1, 1, InputKind.Jump));
                f.Engine.Bind(new SessionBinding(1, 2, 1, 0, true)); f.Pump(10000);
                Check(f.Engine.DrainInputReceipts().Single().Result == Admission.WrongSession, "old epoch accepted");
                f.Engine.ReceiveInput(f.Sessions[1], new PlayerInput(new InputKey(99, 2, 1, 1), 0, 1, InputKind.Jump)); f.Pump(10000);
                Check(f.Engine.DrainInputReceipts().Single().Result == Admission.WrongEncounter, "other encounter accepted");
            });
            Run("future timestamp and clock version forgery rejected", () => {
                var f = new Fixture(); f.Submit(new PlayerInput(f.Key(1, 1), 100000, 1, InputKind.Jump));
                f.Submit(new PlayerInput(f.Key(1, 2), 0, 999, InputKind.Jump)); f.Pump(10000);
                var r = f.Engine.DrainInputReceipts(); Check(r[0].Result == Admission.FutureTimestamp && r[1].Result == Admission.ClockMismatch, "time forgery");
            });
            Run("clock fallback uses server receipt and cannot backdate", () => {
                var f = new Fixture(); var binding = new SessionBinding(1, 2, 2, 0, false); f.Engine.Bind(binding);
                f.Now = 50000; f.Engine.ReceiveInput(binding, new PlayerInput(f.Key(1, 1, 2), 0, 2, InputKind.Jump)); f.Pump(60000);
                var r = f.Engine.DrainInputReceipts().Single(); Check(r.UsedReceiveTime && r.EffectiveTick == 6, "fallback backdated input");
            });
            Run("reordered inputs admitted but contradictory sequence time rejected", () => {
                var f = new Fixture(); f.Now = 30000;
                f.Submit(f.Input(1, 2, 3, InputKind.Move, 1)); f.Submit(f.Input(1, 1, 2, InputKind.Jump));
                f.Submit(f.Input(1, 3, 1, InputKind.Jump)); f.Pump(40000);
                var r = f.Engine.DrainInputReceipts(); Check(r[0].Result == Admission.Admitted && r[1].Result == Admission.Admitted && r[2].Result == Admission.SequenceTimeConflict, "sequence ordering");
            });
            Run("non-host proposals and altered proposals rejected", () => {
                var f = new Fixture(); var p = new HostProposal(77, 1, 1, 3, 100, 2);
                f.Engine.ReceiveProposal(f.Sessions[1], p); f.Engine.ReceiveProposal(f.Sessions[0], p);
                f.Engine.ReceiveProposal(f.Sessions[0], p); f.Engine.ReceiveProposal(f.Sessions[0], new HostProposal(77, 1, 1, 4, 100, 3)); f.Pump(10000);
                var r = f.Engine.DrainProposalReceipts(); Check(r[0].Result == Admission.WrongHost && r[1].Result == Admission.Admitted && r[2].Result == Admission.Duplicate && r[3].Result == Admission.Conflict, "host authority");
            });
            Run("host cannot invent a participant skill without original input", () => {
                var f = new Fixture(); f.Engine.ReceiveProposal(f.Sessions[0], new HostProposal(77, 1, 1, 1, 2, 1, f.Key(2, 99))); f.Pump(110000);
                Check(f.Engine.FinalSnapshot.Fighters[100].Hp == 1000, "invented attack");
                Check(f.Engine.DrainCommittedEvents().Any(x => x.Kind == "ActionRejected"), "missing rejection");
            });
            Run("movement lease expires after packet loss", () => {
                var f = new Fixture(); f.Submit(f.Input(1, 1, 1, InputKind.Move, 1)); f.Pump(150000);
                Check(f.Engine.FinalSnapshot.Fighters[1].X == 300, "movement held forever");
            });
            Run("concurrent ingress snapshot includes every stamped command", () => {
                var f = new Fixture(); Parallel.For(1, 5, id => f.Submit(f.Input(id, 1, 1, InputKind.Jump))); f.Pump(100000);
                Check(f.Engine.DrainInputReceipts().Length == 4, "lost ingress");
                Check(f.Engine.DrainCommittedEvents().Count(x => x.Kind == "InputCommitted") == 4, "lost finalized inputs");
            });
            Run("queue overload, replay budget and clock reversal fail closed", () => {
                var f = new Fixture(1); f.Submit(f.Input(1, 1, 1, InputKind.Jump)); f.Submit(f.Input(2, 1, 1, InputKind.Jump)); f.Engine.Pump();
                Check(f.Engine.IsAborted && f.Engine.FinalizedThroughTick == 0, "overflow approved input");
                f = new Fixture(); f.Now = 3000000; f.Engine.Pump(); Check(f.Engine.IsAborted, "unbounded replay");
                f = new Fixture(); f.Pump(10000); f.Now = 0; f.Engine.Pump(); Check(f.Engine.IsAborted, "backwards clock");
            });
            Run("abort discards provisional state without publishing outcomes", () => {
                var f = new Fixture(); f.Boss(); f.Pump(50000); f.Engine.Abort("Host departed"); f.PumpAborted(150000);
                Check(f.Engine.FinalSnapshot.Fighters[1].Hp == 100 && f.Engine.DrainCommittedEvents().Length == 0, "aborted damage committed");
            });
            Run("prediction confirms without replay, repeated event and key stay idempotent", () => {
                var ledger = new PredictionLedger(77, 1, 1);
                var key = new FeedbackKey(new InputKey(77, 1, 1, 1), 0, 100, 0);
                Check(ledger.Predict(key, 100) == FeedbackDecision.Start, "prediction not started");
                Check(ledger.Predict(key, 100) == FeedbackDecision.None, "prediction repeated");
                Check(ledger.Resolve(1, key, true) == FeedbackDecision.ConfirmExisting, "confirmation replayed effect");
                Check(ledger.Resolve(1, key, true) == FeedbackDecision.None && ledger.Resolve(2, key, true) == FeedbackDecision.None, "duplicate hit replayed");
                Check(ledger.Predict(new FeedbackKey(key.Input, 0, 100, 1), 100) == FeedbackDecision.Start, "multi-hit round suppressed");
            });
            Run("wrong prediction cancels only its token and timeout leaves tombstone", () => {
                var ledger = new PredictionLedger(77, 1, 1); var input = new InputKey(77, 1, 1, 1);
                var a = new FeedbackKey(input, 0, 100, 0); var b = new FeedbackKey(input, 0, 100, 1);
                ledger.Predict(a, 10); ledger.Predict(b, 30);
                Check(ledger.Resolve(1, a, false) == FeedbackDecision.CancelExisting, "miss not cancelled");
                Check(ledger.ExpireUnconfirmed(20).Length == 0, "other token cancelled");
                Check(ledger.ExpireUnconfirmed(30).Single().Equals(b), "timeout not scoped");
                Check(ledger.Resolve(2, b, true) == FeedbackDecision.ConfirmExisting, "late confirmation replayed sound");
            });
            Run("prediction reconnect discards old epoch, unrelated targets remain independent", () => {
                var ledger = new PredictionLedger(77, 1, 1); var input = new InputKey(77, 1, 1, 1);
                var a = new FeedbackKey(input, 0, 100, 0); var b = new FeedbackKey(input, 0, 101, 0);
                ledger.Predict(a, 10); ledger.Predict(b, 10);
                Check(ledger.RejectAction(input).Length == 2, "action cleanup incomplete");
                ledger.Reset(77, 2); Check(ledger.Resolve(1, a, true) == FeedbackDecision.None, "old epoch resurrected feedback");
                Check(ledger.Resolve(2, new FeedbackKey(new InputKey(77, 1, 2, 1), 0, 100, 0), true) == FeedbackDecision.Start, "unpredicted confirmed hit lost");
            });
            Run("host proposal has a distinct strict deadline", () => {
                foreach (long receive in new[] { 69999L, 70000L }) {
                    var f = new Fixture(); f.Attack(1, 1, 1); f.Pump(10000);
                    // Use a fresh key with a valid origin but the same original evaluation tick.
                    f.Now = receive; f.Engine.ReceiveProposal(f.Sessions[0], new HostProposal(77, 1, 99, 1, 1, 1, f.Key(1, 1))); f.Pump(receive);
                    Check(f.Engine.DrainProposalReceipts().Last().Result == (receive < 70000 ? Admission.Admitted : Admission.TooLate), "proposal deadline edge");
                }
            });
            Run("near-future clock tolerance clamps to receipt, invalid policy fails", () => {
                var f = new Fixture(); f.Submit(new PlayerInput(f.Key(1, 1), 999, 1, InputKind.Jump)); f.Pump(10000);
                Check(f.Engine.DrainInputReceipts().Single().EffectiveTick == 1, "future clamp");
                bool rejected = false; try { new TimingPolicy(10000, 40000, 20000, 50000, 1000); } catch (ArgumentOutOfRangeException) { rejected = true; }
                Check(rejected, "finalized before proposals could arrive");
            });
            Run("rejected retransmission preserves its original rejection", () => {
                var f = new Fixture(); var input = f.Input(1, 1, 1, InputKind.Jump);
                f.Now = 50000; f.Submit(input); f.Pump(50000); f.Submit(input); f.Pump(50000);
                var r = f.Engine.DrainInputReceipts();
                Check(r[1].Result == Admission.Duplicate && r[1].OriginalResult == Admission.TooLate, "retry hid rejection");
            });
            Run("20 shuffled four-player input histories match ordered final states and events", () => {
                var ordered = new Fixture(); ordered.Boss();
                for (int tick = 2; tick <= 3; tick++) {
                    ordered.Now = (tick - 1) * 10000;
                    for (int id = 1; id <= 4; id++) ordered.Submit(ordered.Input(id, tick - 1, tick, tick == 2 ? InputKind.Move : InputKind.Jump, tick == 2 ? 1 : 0));
                    ordered.Pump(tick * 10000);
                }
                ordered.Pump(140000);
                var expectedEvents = string.Join(";", ordered.Engine.DrainCommittedEvents().Select(x => x.Kind + ":" + x.Key + ":" + x.Damage));
                for (int seed = 0; seed < 20; seed++) {
                    var f = new Fixture(); f.Boss(); f.Pump(40000); f.Now = 45000;
                    var random = new Random(seed); var items = new List<PlayerInput>();
                    for (int id = 1; id <= 4; id++) { items.Add(f.Input(id, 1, 2, InputKind.Move, 1)); items.Add(f.Input(id, 2, 3, InputKind.Jump)); }
                    foreach (var input in items.OrderBy(_ => random.Next())) { f.Submit(input); f.Submit(input); }
                    f.Pump(50000); f.Pump(140000); Compare(f.Engine.FinalSnapshot, ordered.Engine.FinalSnapshot);
                    var actualEvents = string.Join(";", f.Engine.DrainCommittedEvents().Select(x => x.Kind + ":" + x.Key + ":" + x.Damage));
                    Check(actualEvents == expectedEvents, "delivery order changed final events; seed=" + seed);
                }
            });
            Console.WriteLine($"PASS {Passed.Count} scenarios; runtime={Environment.Version}");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static void PumpAborted(this Fixture f, long now) { f.Now = now; f.Engine.Pump(); }
    private static Fixture Scenario(bool onTime)
    {
        var f = new Fixture(); f.Boss();
        if (onTime) { f.Now = 20000; f.Submit(f.Input(1, 1, 3, InputKind.Jump)); }
        f.Now = 40000; f.Attack(1, 2, 5); f.Pump(60000); return f;
    }
    private static void Compare(CombatState a, CombatState b)
    {
        Check(a.Tick == b.Tick, "state tick");
        foreach (var id in a.Fighters.Keys) {
            var x = a.Fighters[id]; var y = b.Fighters[id];
            Check(x.Hp == y.Hp && x.X == y.X && x.JumpHeight == y.JumpHeight && x.StunnedUntil == y.StunnedUntil,
                "replay differs from ordered run for entity " + id);
        }
    }
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static void Run(string name, Action test) { test(); Passed.Add(name); Console.WriteLine("PASS " + name); }
}
