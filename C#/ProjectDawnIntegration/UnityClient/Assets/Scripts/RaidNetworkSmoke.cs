using System;
using UnityEngine;

namespace DnfRaidDemo
{
    /// <summary>Opt-in executable smoke test. It only uses the local loopback interface.</summary>
    public sealed class RaidNetworkSmoke : MonoBehaviour
    {
        private bool host;
        private RaidNetwork network;
        private RaidSimulation sim;
        private float started, connectedAt = -1, accumulator;
        private bool connectedEver, skillSent, jumpSent, sawMove, sawAirborne, sawActionState, sawLandingState, tickRegressed;
        private int arcPresses, jumpPresses, lastTick;
        private float largestClientX = -4;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            string[] args = Environment.GetCommandLineArgs();
            bool isHost = Array.IndexOf(args, "--net-test-host") >= 0;
            bool isClient = Array.IndexOf(args, "--net-test-client") >= 0;
            if (!isHost && !isClient) return;
            var owner = new GameObject("Localhost network smoke test");
            DontDestroyOnLoad(owner);
            owner.AddComponent<RaidNetworkSmoke>().host = isHost;
        }

        private void Start()
        {
            Application.runInBackground = true;
            Application.targetFrameRate = 120;
            started = Time.realtimeSinceStartup;
            network = new RaidNetwork();
            sim = new RaidSimulation(2);
            sim.PracticeInvulnerable = true;
            if (host) { sim.Start(); network.StartHost(27177); }
            else network.StartClient("127.0.0.1", 27177);
            Debug.Log("[DNF_NET_TEST_STARTED] role=" + (host ? "host" : "client"));
        }

        private void Update()
        {
            network.Poll(sim);
            if (network.Connected)
            {
                connectedEver = true;
                if (connectedAt < 0) connectedAt = Time.realtimeSinceStartup;
            }
            if (host)
            {
                accumulator += Mathf.Min(Time.unscaledDeltaTime, .1f);
                while (accumulator >= RaidSimulation.FixedStep)
                {
                    accumulator -= RaidSimulation.FixedStep;
                    PlayerCommand remote = network.ConsumeRemoteInput();
                    sawMove |= remote.Move.x > .5f;
                    if (remote.Pressed(SkillId.BloodSword)) arcPresses++;
                    if (remote.Jump) jumpPresses++;
                    sim.Tick(RaidSimulation.FixedStep, new[] { new PlayerCommand { PlayerId = 0 }, remote });
                }
                network.SendSnapshot(sim);
            }
            else if (network.Connected)
            {
                float age = Time.realtimeSinceStartup - connectedAt;
                var command = new PlayerCommand
                {
                    PlayerId = 1,
                    Move = age < 1.2f ? Vector2.right : Vector2.zero,
                    Skills = !skillSent && age > .5f ? PlayerCommand.Bit(SkillId.BloodSword) : 0,
                    Jump = !jumpSent && age > 1.7f
                };
                if (command.Pressed(SkillId.BloodSword)) skillSent = true;
                if (command.Jump) jumpSent = true;
                network.SendInput(command);
                if (sim.TickIndex < lastTick) tickRegressed = true;
                lastTick = sim.TickIndex;
                largestClientX = Mathf.Max(largestClientX, sim.Players[1].Position.x);
                sawAirborne |= sim.Players[1].JumpHeight > .05f;
                PlayerState replica = sim.Players[1];
                sawActionState |= replica.ActiveSkill == SkillId.BloodSword && replica.ActionSequence > 0
                    && replica.ActionDuration > 0 && replica.ActionElapsed <= replica.ActionDuration + .0001f;
                sawLandingState |= replica.LandingTime > 0;
            }
            if (Time.realtimeSinceStartup - started >= (host ? 12f : 9f)) Finish();
        }

        private void Finish()
        {
            bool passed = host
                ? connectedEver && sawMove && arcPresses == 1 && jumpPresses == 1 && sim.TickIndex > 120
                : connectedEver && network.ReceivedSnapshots > 30 && largestClientX > -2f
                    && sawAirborne && sawActionState && sawLandingState && !tickRegressed && sim.TickIndex > 120;
            Debug.Log((passed ? "[DNF_NET_TEST_PASSED]" : "[DNF_NET_TEST_FAILED]")
                + " role=" + (host ? "host" : "client") + "; connected=" + connectedEver
                + "; move=" + sawMove + "; skillPresses=" + arcPresses
                + "; jumpPresses=" + jumpPresses + "; airborneSnapshot=" + sawAirborne
                + "; actionSnapshot=" + sawActionState + "; landingSnapshot=" + sawLandingState
                + "; snapshots=" + network.ReceivedSnapshots + "; tick=" + sim.TickIndex
                + "; maxClientX=" + largestClientX + "; tickRegressed=" + tickRegressed);
            network.Dispose();
            enabled = false;
            Application.Quit(passed ? 0 : 1);
        }

        private void OnDestroy() { if (network != null) network.Dispose(); }
    }
}
