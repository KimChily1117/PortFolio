using Kimchily.Server.Core;

static class PortalChecks
{
    public static async Task RunWire(Uri address, Action<bool, string> assert, Action<string> passed)
    {
        ClientCommand Join(string room) => new()
        { ProtocolVersion = 1, Type = "join", Name = "포털 확인", WorldId = "island", RevisionId = "wire", RoomId = room };
        ClientCommand Game(string action) => new()
        { ProtocolVersion = 1, Type = "game", Preset = "chili-portal-v1", Action = action };
        ClientCommand Pose(long sequence, float x) => new()
        { ProtocolVersion = 1, Type = "state", State = new(sequence, x, .15f, 2, 0, 0, true, 0) };
        async Task<PortalGameState> UntilGame(Wire peer, string phase)
        {
            var deadline = DateTime.UtcNow.AddSeconds(7);
            while (DateTime.UtcNow < deadline)
            {
                var item = await peer.Next();
                assert(item.Type != "error", $"Game wire error: {item.Code}");
                if (item.Game is { } game && game.Phase == phase) return game;
            }
            throw new TimeoutException("Timed out waiting for game phase " + phase);
        }

        await using var alice = await Wire.Connect(address);
        await using var bob = await Wire.Connect(address);
        await alice.Send(Join("portal-wire")); await alice.Expect("joined");
        await bob.Send(Join("portal-wire")); await bob.Expect("joined"); await alice.Expect("playerJoined");
        await alice.Send(Game("watch"));
        assert((await UntilGame(alice, "waiting")).RequiredPlayers == 2 && (await UntilGame(bob, "waiting")).RequiredPlayers == 2,
            "Real watch did not synchronize both waiting clients.");
        await alice.Send(Game("start")); await UntilGame(alice, "playing"); await UntilGame(bob, "playing");
        await alice.Send(Pose(1, -3)); await bob.Send(Pose(1, 3));
        await UntilGame(alice, "holding"); await UntilGame(bob, "holding");
        for (var step = 1; step <= 6; step++)
        {
            await Task.Delay(500);
            await alice.Send(Pose(step + 1, -3)); await bob.Send(Pose(step + 1, 3));
        }
        var first = await UntilGame(alice, "complete");
        var second = await UntilGame(bob, "complete");
        assert(first.Version == second.Version && first.RemainingMs == 0 && second.Pads.Count(pad => pad.PlayerId is not null) == 2,
            "Real clients did not receive the same server-decided victory.");
        await using var late = await Wire.Connect(address);
        await late.Send(Join("portal-wire"));
        assert((await late.Expect("joined")).Game is { Phase: "complete", RequiredPlayers: 2 }, "Real late join lost completed puzzle state.");
        await late.Raw("{\"protocolVersion\":1,\"type\":\"game\",\"preset\":\"chili-portal-v1\",\"action\":\"reset\",\"requiredPlayers\":1}");
        assert((await late.Expect("error")).Code == "INVALID_MESSAGE", "Client-supplied puzzle rules were accepted.");
        await late.Send(Game("reset"));
        assert((await UntilGame(late, "waiting")).RequiredPlayers == 3, "Real replay reset did not use current member preview.");
        passed("Real WebSocket players charge the same server-timed portal, share victory and replay, and reject client rule overrides");

        await using var idle = await Wire.Connect(address);
        await idle.Send(Join("portal-idle")); await idle.Expect("joined");
        await idle.Send(Game("start")); await UntilGame(idle, "playing");
        await idle.Send(Pose(1, -3)); await UntilGame(idle, "holding");
        var expired = await UntilGame(idle, "playing");
        assert(expired.RemainingMs == 3000 && expired.Pads.All(pad => pad.PlayerId is null), "Actual host pump did not expire silent pose occupancy.");
        passed("The real host timer releases stale pads without relying on further client messages");
    }

    public static async Task Run(Action<bool, string> assert, Action<string> passed)
    {
        long now = 0;
        var hub = new RoomHub(() => now);
        ClientCommand Join(string room = "portal") => new()
        { ProtocolVersion = 1, Type = "join", Name = "칠리", WorldId = "island", RevisionId = "r1", RoomId = room };
        ClientCommand Game(string action) => new()
        { ProtocolVersion = 1, Type = "game", Preset = "chili-portal-v1", Action = action };
        async Task Dispatch(FakePeer peer, ClientCommand command)
        { var task = hub.HandleAsync(peer, command); hub.Flush(); await task; }
        async Task Pose(FakePeer peer, long sequence, float x, float z, bool grounded = true, float y = .15f)
        { await Dispatch(peer, new() { ProtocolVersion = 1, Type = "state", State = new(sequence, x, y, z, 0, 0, grounded, 0) }); }
        void Tick() { assert(hub.TryQueueGameTick(), "Could not queue game tick."); hub.Flush(); }
        PortalGameState State(FakePeer peer) => peer.Events.Last(item => item.Game is not null).Game!;

        var alice = new FakePeer("portal-a"); var bob = new FakePeer("portal-b"); var late = new FakePeer("portal-c");
        await Dispatch(alice, Game("start"));
        assert(alice.Events.Last().Code == "NOT_JOINED", "Unjoined game command accepted.");
        await Dispatch(alice, Join()); await Dispatch(bob, Join());
        assert(alice.Events.Single(item => item.Type == "joined").Game is null, "Ordinary room was opted into a game implicitly.");
        await Dispatch(alice, Game("watch") with { Preset = "unregistered-rules" });
        assert(alice.Events.Last().Code == "INVALID_GAME", "Unknown game preset accepted.");
        await Dispatch(alice, Game("finish"));
        assert(alice.Events.Last().Code == "INVALID_GAME", "Client could request victory.");
        passed("Portal rules require room membership and a registered preset with an allowed action");

        var stranger = new FakePeer("portal-other"); await Dispatch(stranger, Join("other"));
        var otherEvents = stranger.Events.Count;
        await Dispatch(alice, Game("watch"));
        assert(State(alice) is { Phase: "waiting", Round: 0, RequiredPlayers: 2, RemainingMs: 3000 }, "Watch started a round or missed membership.");
        assert(State(bob).Pads.Count(pad => pad.Active) == 2 && stranger.Events.Count == otherEvents, "Game escaped its room or active preview is wrong.");
        var beforeWatch = bob.Events.Count;
        await Dispatch(alice, Game("watch"));
        assert(bob.Events.Count == beforeWatch, "Repeated watch needlessly broadcast unchanged state.");
        passed("Watching opts the room into waiting state and repeated watches return a private snapshot");

        await Dispatch(alice, Game("start"));
        assert(State(alice) is { Phase: "playing", Round: 1, RequiredPlayers: 2 }, "Start did not lock the round count.");
        await Dispatch(late, Join());
        assert(late.Events.Last().Game is { RequiredPlayers: 2, Round: 1 }, "Late join lacked game snapshot or changed a running round.");
        await Dispatch(bob, Game("start"));
        assert(State(bob).Round == 1, "A simultaneous start restarted an active round.");
        passed("Explicit start locks one to four required players and late joins cannot alter the running round");

        await Pose(alice, 1, -3, 2);
        await Pose(bob, 1, -3, 2);
        assert(State(alice).Pads.Count(pad => pad.PlayerId is not null) == 1 && State(alice).Phase == "playing", "One pad or one player satisfied multiple requirements.");
        now = 500; await Pose(bob, 2, 3, 2, grounded: false);
        assert(State(alice).Phase == "playing", "Airborne player occupied a pad.");
        now = 1000; await Pose(bob, 3, 3, 2, y: 3);
        assert(State(alice).Phase == "playing", "A player too high above a pad occupied it.");
        now = 1500; await Pose(alice, 2, -3, 2); await Pose(bob, 4, 3, 2);
        assert(State(alice) is { Phase: "holding", RemainingMs: 3000 }, "Distinct grounded occupants did not start the hold.");
        passed("Server pad occupancy requires distinct nearby grounded players at the authored pad height");

        now = 2500; await Pose(alice, 3, -3, 2); await Pose(bob, 5, 3, 2); Tick();
        assert(State(alice) is { Phase: "holding", RemainingMs: 2000 }, "Hold countdown is not server timed.");
        now = 2700; await Pose(bob, 6, 5, 2);
        assert(State(alice) is { Phase: "playing", RemainingMs: 3000 }, "Leaving a pad did not reset the hold.");
        now = 2900; await Pose(bob, 7, 3, 2);
        assert(State(alice) is { Phase: "holding", RemainingMs: 3000 }, "Returning to a pad resumed stale countdown progress.");
        now = 4201; Tick();
        assert(State(alice) is { Phase: "playing", RemainingMs: 3000 } && State(alice).Pads.All(pad => pad.PlayerId is null), "Stale stationary clients kept charging the portal.");
        passed("Leaving a pad or expiring a pose resets progress even when no network commands arrive");

        now = 4500; await Pose(alice, 4, -3, 2); await Pose(bob, 8, 3, 2);
        for (var step = 1; step <= 6; step++)
        {
            now = 4500 + step * 500;
            await Pose(alice, 4 + step, -3, 2); await Pose(bob, 8 + step, 3, 2); Tick();
            assert(step == 6 ? State(alice).Phase == "complete" : State(alice).Phase == "holding", "Victory occurred before or after the three-second hold.");
        }
        var completed = State(alice);
        now = 9000; await Pose(alice, 11, 0, 0); Tick();
        assert(State(alice) == completed && completed.RemainingMs == 0, "Moving or stale poses closed the completed portal.");
        var spectator = new FakePeer("portal-d"); await Dispatch(spectator, Join());
        assert(spectator.Events.Last().Game == completed, "Late joiner did not receive permanent success.");
        passed("Three continuous seconds open the portal permanently and completed snapshots reach late joiners");

        await Dispatch(alice, Game("reset"));
        assert(State(alice) is { Phase: "waiting", Round: 1, RequiredPlayers: 4, RemainingMs: 3000 }, "Reset did not return to waiting with current preview.");
        await Dispatch(alice, Game("start"));
        assert(State(alice) is { Phase: "playing", Round: 2, RequiredPlayers: 4 }, "Replay did not create a fresh round with all four pads.");
        var disconnect = hub.DisconnectAsync(bob); hub.Flush(); await disconnect;
        assert(State(alice).RequiredPlayers == 4, "Departure silently reduced a running round requirement.");
        passed("Replay explicitly resets the round; requirements remain locked after departures until reset");

        await Dispatch(alice, Game("reset")); await Dispatch(alice, Game("start"));
        var snapshot = State(alice);
        await Dispatch(alice, Game("reset"));
        assert(alice.Events.Last().Code == "GAME_RATE_LIMIT" && State(alice) == snapshot, "Game control flood mutated the state or escaped the limit.");
        now += 5000; await Dispatch(alice, Game("reset"));
        assert(State(alice).Phase == "waiting", "Game command cooldown never recovers.");
        passed("Game control rate limiting rejects reset floods without changing the shared state and then recovers");

        var solo = new FakePeer("solo"); await Dispatch(solo, Join("solo")); await Dispatch(solo, Game("start"));
        assert(State(solo).RequiredPlayers == 1, "Solo test was blocked by a multiplayer minimum.");
        await Pose(solo, 1, -3, 2);
        for (var step = 1; step <= 6; step++) { now += 500; await Pose(solo, step + 1, -3, 2); Tick(); }
        assert(State(solo).Phase == "complete", "Solo player could not complete the authored first pad.");
        disconnect = hub.DisconnectAsync(solo); hub.Flush(); await disconnect;
        await Dispatch(solo, Join("solo"));
        assert(solo.Events.Last().Game is null, "Empty room retained the previous game's success.");
        passed("The solo demo can finish, and removing the last member removes all game state");

        var crowd = Enumerable.Range(0, 8).Select(index => new FakePeer("crowd-" + index)).ToArray();
        foreach (var person in crowd) await Dispatch(person, Join("crowd"));
        await Dispatch(crowd[0], Game("start"));
        assert(State(crowd[0]).RequiredPlayers == 4 && State(crowd[0]).Pads.All(pad => pad.Active), "Room capacity above four made the puzzle impossible.");
        assert(stranger.Events.Count == otherEvents, "Game events leaked to a separate room.");
        passed("Larger demo rooms cap the puzzle at four pads while keeping game events isolated");
    }
}
