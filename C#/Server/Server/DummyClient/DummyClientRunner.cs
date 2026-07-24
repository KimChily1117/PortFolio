using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace DummyClient
{
    public sealed class DummyClientRunner
    {
        private readonly DummyClientOptions _options;
        private readonly List<DummyClient> _clients = new List<DummyClient>();

        public DummyClientRunner(DummyClientOptions options)
        {
            _options = options;
        }

        public async Task<int> RunAsync()
        {
            IPEndPoint endPoint;
            if (!TryCreateEndPoint(_options.Host, _options.Port, out endPoint))
                return 1;

            CombatTestSummaryReporter combatSummary = null;
            if (_options.CombatSummaryEnabled)
            {
                combatSummary = new CombatTestSummaryReporter(_options);
                await combatSummary.CaptureBaselineAsync();
            }

            Console.WriteLine($"Scenario: {_options.Scenario}");
            Console.WriteLine($"Host={_options.Host}, Port={_options.Port}, Clients={_options.Clients}, Prefix={_options.Prefix}, StartIndex={_options.StartIndex}");
            if (_options.IsTownLoad)
            {
                Console.WriteLine($"HoldInTownSec={_options.HoldInTownSec}, MovementEnabled={_options.EnableMovement}, MovementTransport={_options.MovementTransport}, MovementPattern={_options.MovementPattern}, PatrolMode={_options.GetRequestedPatrolMode()}, MovementIntervalMs={_options.MovementIntervalMs}");
            }

            if (_options.IsMatchScenario)
            {
                Console.WriteLine($"ExpectedExternal={_options.ExpectedExternal}, SceneReadyDelayMs={_options.SceneReadyDelayMs}, HoldAfterDungeonSec={_options.HoldAfterDungeonSec}");
                Console.WriteLine($"MovementEnabled={_options.EnableMovement}, MovementTransport={_options.MovementTransport}, MovementPattern={_options.MovementPattern}, PatrolMode={_options.GetRequestedPatrolMode()}, AttackEnabled={_options.EnableAttack}, CollisionEnabled={_options.EnableCollision}, GameplayDurationSec={_options.GameplayDurationSec}");
            }

            for (int i = 0; i < _options.Clients; i++)
            {
                string name = _options.BuildClientName(i);
                DummyClient client = new DummyClient(_options, i, name);
                _clients.Add(client);
                client.Connect(endPoint);

                if (_options.DelayMs > 0 && i + 1 < _options.Clients)
                    await Task.Delay(_options.DelayMs);
            }

            int gameplayTimeoutSec = _options.HasGameplayEnabled ? _options.GameplayDurationSec + Math.Max(1, _options.GameplayStartDelayMs / 1000) : 0;
            int townLoadTimeoutSec = _options.IsTownLoad ? _options.HoldInTownSec + Math.Max(1, _options.GameplayStartDelayMs / 1000) : 0;
            int scenarioTimeoutSec = _options.IsMatchScenario ? Math.Max(_options.HoldAfterDungeonSec, gameplayTimeoutSec) : townLoadTimeoutSec;
            int totalTimeoutSec = _options.TimeoutSec + scenarioTimeoutSec;
            Task allClients = Task.WhenAll(_clients.Select(c => c.Completion));
            Task timeout = Task.Delay(TimeSpan.FromSeconds(totalTimeoutSec));
            Task completed = await Task.WhenAny(allClients, timeout);

            if (completed == timeout)
            {
                Console.WriteLine($"[DUMMY] Timeout. TimeoutSec={totalTimeoutSec}");
                foreach (DummyClient client in _clients.Where(c => c.State != DummyClientState.Completed && c.State != DummyClientState.Failed))
                    client.Fail("Scenario timeout");
            }

            DummyClientStats stats = new DummyClientStats();
            stats.Print(_options, _clients);
            bool clientsCompleted = _clients.All(c => c.State == DummyClientState.Completed);
            if (combatSummary != null)
                await combatSummary.PrintAsync(_clients, clientsCompleted);
            return clientsCompleted ? 0 : 1;
        }

        private static bool TryCreateEndPoint(string host, int port, out IPEndPoint endPoint)
        {
            endPoint = null;
            try
            {
                IPAddress address;
                if (!IPAddress.TryParse(host, out address))
                {
                    IPAddress[] addresses = Dns.GetHostAddresses(host);
                    address = addresses.FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) ?? addresses.FirstOrDefault();
                }

                if (address == null)
                {
                    Console.WriteLine($"[DUMMY] Could not resolve host: {host}");
                    return false;
                }

                endPoint = new IPEndPoint(address, port);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DUMMY] Host resolve failed. Host={host}, Error={ex.Message}");
                return false;
            }
        }
    }
}


