using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace DummyClient
{
    public sealed class CombatTestSummaryReporter
    {
        private const string ActionLockedReason = "ActionLocked";
        private const string CooldownActiveReason = "CooldownActive";
        private readonly DummyClientOptions _options;
        private readonly HttpClient _httpClient;
        private CombatMetricsSnapshot _baseline;
        private string _error;

        public CombatTestSummaryReporter(DummyClientOptions options)
        {
            _options = options;
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        }

        public async Task CaptureBaselineAsync()
        {
            try
            {
                _baseline = await ReadSnapshotAsync();
            }
            catch (Exception ex)
            {
                _error = $"baseline unavailable: {ex.Message}";
            }
        }

        public async Task PrintAsync(IReadOnlyList<DummyClient> clients, bool clientsCompleted)
        {
            CombatMetricsDelta delta = null;
            if (_baseline != null)
            {
                try
                {
                    // Allow the server to drain the final combat packets before the end snapshot.
                    await Task.Delay(500);
                    CombatMetricsSnapshot end = await ReadSnapshotAsync();
                    delta = CombatMetricsDelta.Between(_baseline, end);
                    if (delta.HasNegativeValue)
                    {
                        _error = "monitoring counters reset during the test";
                        delta = null;
                    }
                }
                catch (Exception ex)
                {
                    _error = $"end snapshot unavailable: {ex.Message}";
                }
            }

            int skillSent = clients.Sum(c => c.SkillSentCount);
            int enteredDungeon = clients.Count(c => c.HasEnteredDungeon);
            string result = GetResult(delta, clients, clientsCompleted, skillSent, enteredDungeon);

            Console.WriteLine();
            Console.WriteLine("[COMBAT TEST SUMMARY]");
            Console.WriteLine($"Scenario              : {(_options.IsNormalCombatTest ? "Normal" : "Spam")}");
            Console.WriteLine($"Clients               : {clients.Count}");
            Console.WriteLine($"AttackIntervalMs      : {_options.AttackIntervalMs}");
            Console.WriteLine($"SkillIds              : {string.Join(",", _options.SkillIds)}");
            Console.WriteLine($"SkillSent             : {skillSent}");
            Console.WriteLine($"ActiveCastRecorded    : {Format(delta?.ActiveCastRecorded)}");
            Console.WriteLine($"ActionLocked          : {Format(delta?.ActionLocked)}");
            Console.WriteLine($"CooldownActive        : {Format(delta?.CooldownActive)}");
            Console.WriteLine($"OtherRejects          : {Format(delta?.OtherRejects)}");
            Console.WriteLine($"EnteredDungeon        : {enteredDungeon}");
            Console.WriteLine($"Result                : {result}");
            if (_error != null)
                Console.WriteLine($"SummaryError          : {_error}");
        }

        private string GetResult(CombatMetricsDelta delta, IReadOnlyList<DummyClient> clients, bool clientsCompleted, int skillSent, int enteredDungeon)
        {
            bool commonSuccess = delta != null &&
                                 clientsCompleted &&
                                 clients.Count == _options.Clients &&
                                 enteredDungeon == clients.Count &&
                                 clients.All(c => c.HasGameplayStarted && c.HasGameplayCompleted) &&
                                 skillSent > 0 &&
                                 delta.OtherRejects == 0;

            if (!commonSuccess)
                return "FAILED";

            if (_options.IsNormalCombatTest &&
                delta.ActionLocked == 0 &&
                delta.CooldownActive == 0 &&
                delta.ActiveCastRecorded == skillSent)
                return "NORMAL FLOW VERIFIED";

            if (_options.IsSpamCombatTest &&
                delta.ActiveCastRecorded > 0 &&
                delta.ActionLocked + delta.CooldownActive > 0 &&
                delta.ActiveCastRecorded + delta.ActionLocked + delta.CooldownActive == skillSent)
                return "SPAM REJECTION VERIFIED";

            return "FAILED";
        }

        private async Task<CombatMetricsSnapshot> ReadSnapshotAsync()
        {
            string json = await _httpClient.GetStringAsync($"{_options.MonitoringApiUrl}/api/events/recent");
            CombatMetricsSnapshot snapshot = JsonSerializer.Deserialize<CombatMetricsSnapshot>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (snapshot == null)
                throw new InvalidOperationException("empty monitoring response");

            return snapshot;
        }

        private static string Format(long? value)
        {
            return value.HasValue ? value.Value.ToString() : "N/A";
        }

        private sealed class CombatMetricsSnapshot
        {
            public long ActiveCastRecorded { get; set; }
            public Dictionary<string, long> RejectReasonCounts { get; set; } = new Dictionary<string, long>(StringComparer.Ordinal);
        }

        private sealed class CombatMetricsDelta
        {
            public long ActiveCastRecorded { get; private set; }
            public long ActionLocked { get; private set; }
            public long CooldownActive { get; private set; }
            public long OtherRejects { get; private set; }
            public bool HasNegativeValue { get; private set; }

            public static CombatMetricsDelta Between(CombatMetricsSnapshot baseline, CombatMetricsSnapshot end)
            {
                IReadOnlyDictionary<string, long> baselineReasons = baseline.RejectReasonCounts ?? new Dictionary<string, long>();
                IReadOnlyDictionary<string, long> endReasons = end.RejectReasonCounts ?? new Dictionary<string, long>();
                Dictionary<string, long> reasons = new Dictionary<string, long>(StringComparer.Ordinal);

                foreach (string reason in baselineReasons.Keys.Concat(endReasons.Keys).Distinct(StringComparer.Ordinal))
                    reasons[reason] = Get(endReasons, reason) - Get(baselineReasons, reason);

                long activeCastRecorded = end.ActiveCastRecorded - baseline.ActiveCastRecorded;
                long actionLocked = Get(reasons, ActionLockedReason);
                long cooldownActive = Get(reasons, CooldownActiveReason);
                long otherRejects = reasons
                    .Where(pair => pair.Key != ActionLockedReason && pair.Key != CooldownActiveReason)
                    .Sum(pair => pair.Value);

                return new CombatMetricsDelta
                {
                    ActiveCastRecorded = activeCastRecorded,
                    ActionLocked = actionLocked,
                    CooldownActive = cooldownActive,
                    OtherRejects = otherRejects,
                    HasNegativeValue = activeCastRecorded < 0 || reasons.Values.Any(value => value < 0)
                };
            }

            private static long Get(IReadOnlyDictionary<string, long> values, string key)
            {
                return values.TryGetValue(key, out long value) ? value : 0;
            }
        }
    }
}
