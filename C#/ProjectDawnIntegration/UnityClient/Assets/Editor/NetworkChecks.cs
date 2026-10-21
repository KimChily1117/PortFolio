using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using DnfRaidDemo;

/// <summary>Protocol behavior tests against real loopback UDP sockets.</summary>
public static class NetworkChecks
{
    [Serializable]
    private class Wire
    {
        public string Magic = "DnfRaidDemo";
        public int V = 6;
        public string Kind;
        public string ClientToken;
        public string Session;
        public long Seq;
        public float X, Y;
        public bool Basic, Run;
        public int[] Press;
    }

    public static void Run()
    {
        using (var host = new RaidNetwork())
        using (var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)))
        using (var unrelated = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)))
        {
            const int port = 27178;
            var endpoint = new IPEndPoint(IPAddress.Loopback, port);
            var sim = new RaidSimulation(2);
            host.StartHost(port);
            Require(host.Role == NetworkRole.Host, "Host socket opened");
            SendBytes(client, endpoint, "{malformed");
            string token = Guid.NewGuid().ToString("N");
            Send(client, endpoint, new Wire { V = 5, Kind = "hello", ClientToken = token });
            Pump(host, sim, .02);
            Require(!host.Connected, "Version 5 build cannot join version 6 host");
            Send(client, endpoint, new Wire { Kind = "hello", ClientToken = token });
            Pump(host, sim, .05);
            Require(host.Connected && client.Available > 0, "Hello accepted after malformed packet");
            IPEndPoint from = endpoint;
            Wire welcome = JsonUtility.FromJson<Wire>(Encoding.UTF8.GetString(client.Receive(ref from)));
            Require(welcome.Kind == "welcome" && welcome.ClientToken == token, "Handshake nonce matches");

            // Sequence 1 is intentionally omitted: the repeated counter in sequence 2 must recover it.
            var command = new Wire { Kind = "input", Session = welcome.Session, Seq = 2,
                X = 1, Run = true, Press = new int[SkillBook.Count + 1] };
            command.Press[(int)SkillId.BloodSword] = 1;
            command.Press[SkillBook.Count] = 1;
            Send(client, endpoint, command);
            Pump(host, sim, .02);
            var first = host.ConsumeRemoteInput();
            Require(first.Pressed(SkillId.BloodSword) && first.Jump && first.Move.x > .99f && first.Run, "Lost initial packet recovered by cumulative counter");
            command.Seq = 3;
            Send(client, endpoint, command);
            Pump(host, sim, .02);
            var repeated = host.ConsumeRemoteInput();
            Require(repeated.Skills == 0 && !repeated.Jump, "Repeated skill and jump counters not consumed twice");

            command.Seq = 1; command.X = -1;
            Send(client, endpoint, command);
            Pump(host, sim, .02);
            Require(host.ConsumeRemoteInput().Move.x > .99f, "Out-of-order input rejected");

            command.Seq = 4;
            Send(unrelated, endpoint, command);
            Pump(host, sim, .02);
            Require(host.ConsumeRemoteInput().Move.x > .99f, "Unpinned endpoint rejected");

            command.V = 99;
            Send(client, endpoint, command);
            Pump(host, sim, .02);
            Require(host.ConsumeRemoteInput().Move.x > .99f, "Unsupported protocol version rejected");

            Pump(host, sim, .4);
            var stale = host.ConsumeRemoteInput();
            Require(stale.Move == Vector2.zero && !stale.Run, "Stale movement stops before disconnect");
            Pump(host, sim, 6.0);
            Require(!host.Connected, "Silent peer times out and frees its slot");
        }
        CheckActionSnapshots();
        Debug.Log("[DNF_NETWORK_CHECKS_PASSED] V6 handshake; malformed JSON; lost packet recovery; duplicate skill/jump; ordering; endpoint pinning; old-version rejection; stale movement; timeout; action snapshot round-trip; malformed action rejection");
    }

    private static void CheckActionSnapshots()
    {
        using (var host = new RaidNetwork())
        using (var client = new RaidNetwork())
        {
            const int port = 27179;
            var authority = ActionFixture();
            var replica = new RaidSimulation(2);
            host.StartHost(port);
            client.StartClient("127.0.0.1", port);
            PumpPair(host, authority, client, replica, .15);
            Require(host.Connected && client.Connected, "Real host/client action snapshot handshake");
            SendSnapshotAndPump(host, authority, client, replica);
            Require(client.ReceivedSnapshots == 1, "Initial V6 action snapshot received");
            Require(!ReferenceEquals(authority.Players[0], replica.Players[0]), "Snapshot deserialized across UDP");
            RequireActionState(authority, replica);

            // A malformed snapshot must be rejected atomically: existing animation state stays intact.
            var acceptedPlayer = replica.Players[0];
            var invalid = new KeyValuePair<string, Action<RaidSimulation>>[]
            {
                Bad("negative player action sequence", s => s.Players[0].ActionSequence = -1),
                Bad("unknown skill enum", s => s.Players[0].ActiveSkill = (SkillId)99),
                Bad("negative combo step", s => s.Players[0].BasicComboStep = -1),
                Bad("combo step above three", s => s.Players[0].BasicComboStep = 4),
                Bad("Frenzy flag on a non-basic skill", s => s.Players[0].ActionFrenzy = true),
                Bad("airborne Frenzy ground combo", s => s.Players[1].ActionAirborne = true),
                Bad("dash attack on non-basic skill", s => s.Players[0].ActionDashAttack = true),
                Bad("dash attack combined with Frenzy combo", s => s.Players[1].ActionDashAttack = true),
                Bad("zero action facing", s => s.Players[0].ActionFacing = 0),
                Bad("negative player elapsed", s => s.Players[0].ActionElapsed = -.1f),
                Bad("nonfinite player elapsed", s => s.Players[0].ActionElapsed = float.NaN),
                Bad("player elapsed past duration", s => s.Players[0].ActionElapsed = 4f),
                Bad("negative player duration", s => s.Players[0].ActionDuration = -.1f),
                Bad("nonfinite player duration", s => s.Players[0].ActionDuration = float.PositiveInfinity),
                Bad("negative landing time", s => s.Players[0].LandingTime = -.01f),
                Bad("nonfinite landing time", s => s.Players[0].LandingTime = float.NaN),
                Bad("negative boss action sequence", s => s.Boss.ActionSequence = -1),
                Bad("negative boss elapsed", s => s.Boss.ActionElapsed = -.1f),
                Bad("nonfinite boss elapsed", s => s.Boss.ActionElapsed = float.PositiveInfinity),
                Bad("boss elapsed past duration", s => s.Boss.ActionElapsed = 9f),
                Bad("negative boss duration", s => s.Boss.ActionDuration = -.1f),
                Bad("nonfinite boss duration", s => s.Boss.ActionDuration = float.NaN)
            };
            foreach (var item in invalid)
            {
                var bad = ActionFixture();
                item.Value(bad);
                SendSnapshotAndPump(host, bad, client, replica);
                Require(client.ReceivedSnapshots == 1 && ReferenceEquals(acceptedPlayer, replica.Players[0]),
                    "Reject malformed snapshot: " + item.Key);
            }
            // Invalid packets do not poison ordering or prevent the next well-formed snapshot.
            authority.Players[0].ActionSequence++;
            authority.Players[0].ActionElapsed = .21f;
            authority.Boss.ActionSequence++;
            authority.TickIndex++;
            SendSnapshotAndPump(host, authority, client, replica);
            Require(client.ReceivedSnapshots == 2, "Valid snapshot accepted after malformed values");
            RequireActionState(authority, replica);
            authority.Players[0].ActionAirborne = false;
            authority.Players[0].ActiveSkill = SkillId.Basic;
            authority.Players[0].ActionDashAttack = true;
            authority.Players[1].Running = true;
            SendSnapshotAndPump(host, authority, client, replica);
            Require(client.ReceivedSnapshots == 3, "Running and dash attack snapshot accepted");
            RequireActionState(authority, replica);
            client.SendInput(new PlayerCommand { PlayerId = 1, Move = Vector2.left, Run = true, Basic = true });
            PumpPair(host, authority, client, replica, .08);
            var run = host.ConsumeRemoteInput();
            Require(run.Run && run.Basic && run.Move.x < -.99f, "Client running attack input round trip");
            Debug.Log("[DNF_ACTION_SNAPSHOT_CHECKS_PASSED] player/boss identity and timing; two players; combo; facing; airborne; landing; rejected=" + invalid.Length);
        }
    }

    private static KeyValuePair<string, Action<RaidSimulation>> Bad(string name, Action<RaidSimulation> mutate)
    {
        return new KeyValuePair<string, Action<RaidSimulation>>(name, mutate);
    }

    private static RaidSimulation ActionFixture()
    {
        var sim = new RaidSimulation(2);
        sim.TickIndex = 73;
        for (int i = 0; i < sim.Players.Count; i++)
        {
            var player = sim.Players[i];
            player.ActiveSkill = i == 0 ? SkillId.BloodSword : SkillId.Basic;
            player.ActionSequence = 7 + i * 6;
            player.BasicComboStep = i == 1 ? 3 : 1;
            player.ActionFrenzy = i == 1;
            // The fourth strike remains dual-wield after its buff expires.
            player.FrenzyTime = 0f; player.BerserkTime = 120f; player.Mp = 73f;
            player.ActionFacing = i == 0 ? 1 : -1;
            player.ActionAirborne = i == 0;
            player.ActionElapsed = .125f + i * .05f;
            player.ActionDuration = .4f + i * .1f;
            player.LandingTime = .03f + i * .04f;
        }
        sim.Players[0].JumpHeight = .5f;
        sim.Players[0].JumpVelocity = -1.2f;
        sim.Boss.ActionSequence = 12;
        sim.Boss.ActionElapsed = .37f;
        sim.Boss.ActionDuration = 1.2f;
        return sim;
    }

    private static void RequireActionState(RaidSimulation expected, RaidSimulation actual)
    {
        Require(expected.TickIndex == actual.TickIndex, "Snapshot tick copied with action state");
        for (int i = 0; i < expected.Players.Count; i++)
        {
            var source = expected.Players[i];
            var copy = actual.Players[i];
            Require(copy.ActiveSkill == source.ActiveSkill && copy.ActionSequence == source.ActionSequence
                && copy.BasicComboStep == source.BasicComboStep && copy.ActionFacing == source.ActionFacing
                && copy.ActionDashAttack == source.ActionDashAttack && copy.Running == source.Running
                && copy.ActionAirborne == source.ActionAirborne && copy.ActionFrenzy == source.ActionFrenzy
                && Near(copy.FrenzyTime, source.FrenzyTime) && Near(copy.BerserkTime, source.BerserkTime) && Near(copy.Mp, source.Mp)
                && Near(copy.ActionElapsed, source.ActionElapsed)
                && Near(copy.ActionDuration, source.ActionDuration) && Near(copy.LandingTime, source.LandingTime),
                "Player " + i + " action identity, combo, direction, airborne, timing and landing round-trip");
        }
        Require(actual.Boss.ActionSequence == expected.Boss.ActionSequence
            && Near(actual.Boss.ActionElapsed, expected.Boss.ActionElapsed)
            && Near(actual.Boss.ActionDuration, expected.Boss.ActionDuration), "Boss action round-trip");
    }

    private static bool Near(float a, float b) { return Mathf.Abs(a - b) < .00001f; }

    private static void SendSnapshotAndPump(RaidNetwork host, RaidSimulation authority, RaidNetwork client, RaidSimulation replica)
    {
        PumpPair(host, authority, client, replica, .06); // Wait through the 20Hz sender throttle.
        host.SendSnapshot(authority);
        PumpPair(host, authority, client, replica, .025);
    }

    private static void PumpPair(RaidNetwork host, RaidSimulation authority, RaidNetwork client, RaidSimulation replica, double seconds)
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        do { host.Poll(authority); client.Poll(replica); Thread.Sleep(2); } while (timer.Elapsed.TotalSeconds < seconds);
    }

    private static void Send(UdpClient client, IPEndPoint endpoint, Wire wire)
    {
        SendBytes(client, endpoint, JsonUtility.ToJson(wire));
    }
    private static void SendBytes(UdpClient client, IPEndPoint endpoint, string json)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        client.Send(bytes, bytes.Length, endpoint);
    }
    private static void Pump(RaidNetwork network, RaidSimulation sim, double seconds)
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        do { network.Poll(sim); Thread.Sleep(2); } while (timer.Elapsed.TotalSeconds < seconds);
    }
    private static void Require(bool condition, string detail)
    {
        if (!condition) throw new InvalidOperationException("Network check failed: " + detail);
    }
}
