using System.Text.Json;
using Kimchily.Server.Core;

/// <summary>실제 등록한 TS를 범용 C# 호스트로 실행한다. 게임 규칙을 C# 테스트에 다시 구현하지 않는다.</summary>
static class RelayChecks
{
    public static async Task Run(Action<bool, string> assert, Action<string> passed)
    {
        long now = 1000;
        var hub = new RoomHub(() => now, new ApprovedScriptCatalog(Path.Combine(PortalChecks.Root, "games")));
        var sequence = new Dictionary<string, long>();
        var alice = new FakePeer("relay-a"); var bob = new FakePeer("relay-b"); var carol = new FakePeer("relay-c");
        async Task Send(FakePeer peer, ClientCommand command)
        {
            var count = peer.Events.Count;
            var task = hub.HandleAsync(peer, command); hub.Flush(); await task;
            assert(!peer.Events.Skip(count).Any(e => e.Type == "error"), "Unexpected relay error: " + peer.Events.Last().Code);
        }
        Task Join(FakePeer peer) => Send(peer, new() { ProtocolVersion = 1, Type = "join", Name = peer.Id,
            WorldId = "chili-island", RevisionId = "relay-check", RoomId = "relay" });
        Task Command(string action) => Send(alice, new() { ProtocolVersion = 1, Type = "game",
            ScriptId = PortalChecks.ScriptId, ScriptHash = PortalChecks.ScriptHash, Action = action });
        Task Pose(FakePeer peer, float x, float z, bool grounded = true, float y = .15f)
        {
            sequence.TryGetValue(peer.Id, out var value); sequence[peer.Id] = ++value;
            return Send(peer, new() { ProtocolVersion = 1, Type = "state", State = new(value, x, y, z, 0, 0, grounded, 0) });
        }
        void Tick() { assert(hub.TryQueueGameTick(), "Relay timer was not queued."); hub.Flush(); }
        JsonElement State(FakePeer? peer = null) => JsonSerializer.Deserialize<JsonElement>((peer ?? alice).Events.Last(e => e.Game is not null).Game!.StateJson);
        string Phase() => State().GetProperty("phase").GetString()!;
        int Step() => State().GetProperty("relayStep").GetInt32();
        bool Entered(FakePeer peer) => State().GetProperty("enteredPlayerIds").EnumerateArray().Any(id => id.GetString() == peer.Id);
        async Task Hold(FakePeer peer, float x, float z)
        {
            await Pose(peer, x, z);
            for (int i = 0; i < 3; i++) { now += 500; await Pose(peer, x, z); Tick(); }
        }

        await Join(alice); await Join(bob); await Join(carol); await Command("watch"); await Command("start");
        await Pose(alice, 0, 10);
        assert(!Entered(alice) && Step() == 0, "Closed portal admitted a player.");
        now += 1000;
        await Pose(alice, -3, 2); await Pose(bob, 3, 2); await Pose(carol, -3, 6);
        for (int i = 0; i < 6; i++) {
            now += 500; await Pose(alice, -3, 2); await Pose(bob, 3, 2); await Pose(carol, -3, 6); Tick();
        }
        assert(Phase() == "complete", "Three-player first mission did not open portal.");
        now += 2000; await Hold(alice, -3, 34);
        assert(Step() == 0 && !Entered(alice), "Skipping the portal activated the second mission.");
        passed("Three-player portal unlock does not admit players early or allow bypassing the portal for relay credit");

        now += 2000; await Pose(alice, 0, 10);
        now += 100; await Pose(bob, 0, 10, y: 4);
        assert(Entered(alice) && !Entered(bob), "Portal ignored authored height bounds.");
        now += 100; await Pose(bob, 0, 10, grounded: false);
        assert(!Entered(bob), "Airborne player entered the portal.");
        now += 100; await Pose(bob, 0, 10); await Pose(carol, 0, 10);
        assert(Entered(bob) && Entered(carol) && Phase() == "relay", "Valid portal entrants were not admitted.");
        passed("Server TypeScript admits grounded portal entrants and synchronizes the second mission");

        now += 2000; await Hold(alice, 3, 34);
        assert(Step() == 0, "Moon advanced before the star.");
        now += 500; await Hold(alice, -3, 34);
        assert(Step() == 1, "Star was not charged by an admitted participant.");
        now += 500; await Hold(alice, 3, 34);
        assert(Step() == 1, "The first participant consumed another participant's relay turn.");
        passed("Relay respects authored pad order and requires distinct initial contributors for three participants");

        now += 2000; await Hold(bob, 3, 34);
        assert(Step() == 2, "Second contributor failed to charge moon.");
        var late = new FakePeer("relay-late"); await Join(late);
        assert(State(late).GetProperty("relayStep").GetInt32() == 2 && !Entered(late), "Late arrival lost progress or bypassed entry.");
        await Hold(late, -3, 38);
        assert(Step() == 2, "Unadmitted late arrival advanced the relay.");
        passed("Late join receives shared relay progress but must enter the open portal before contributing");

        now += 2000; await Pose(carol, -3, 38);
        now += 500; await Pose(carol, -3, 38);
        assert(Phase() == "relayHolding", "Third contributor could not begin charge.");
        now += 1300; Tick();
        assert(Phase() == "relay" && Step() == 2 && State().GetProperty("relayRemainingMs").GetInt32() == 1500,
            "Stale relay pose kept charging or erased already completed steps.");
        now += 100; await Hold(carol, -3, 38);
        assert(Step() == 3, "Fresh relay attempt failed after expiration.");
        now += 500; await Hold(carol, 3, 38);
        assert(Step() == 3, "Consecutive multiplayer turns bypassed handoff after all contributors joined.");
        passed("Stale relay poses reset only the current charge and subsequent multiplayer turns require handoff");

        now += 1000; await Hold(alice, 3, 38);
        assert(Phase() == "finished" && Step() == 4, "Four ordered lights did not finish both missions.");
        var finalJson = alice.Events.Last(e => e.Game is not null).Game!.StateJson;
        assert(bob.Events.Last(e => e.Game is not null).Game!.StateJson == finalJson
            && carol.Events.Last(e => e.Game is not null).Game!.StateJson == finalJson, "Participants received different completion state.");
        now += 2000; Tick();
        assert(Phase() == "finished", "Completed relay was lost after positions expired.");
        passed("All clients share a permanent two-mission completion snapshot after the fourth relay light");

        await Command("reset");
        assert(Phase() == "waiting" && Step() == 0 && State().GetProperty("enteredPlayerIds").GetArrayLength() == 0
            && State().GetProperty("relayContributors").GetArrayLength() == 0, "Replay retained previous entry or contribution data.");
        passed("Replay clears both mission progress and portal admission while preserving the common room");
    }
}
