using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Server.Monitoring
{
    public static class CombatRejectMetrics
    {
        private static readonly ConcurrentDictionary<string, long> RejectCounts = new ConcurrentDictionary<string, long>(StringComparer.Ordinal);

        public static void Increment(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                reason = "Unknown";

            RejectCounts.AddOrUpdate(reason, 1, (_, current) => current + 1);
            RecentEventBuffer.Add(new RecentEventSnapshot
            {
                Type = "SkillCastRejected",
                Reason = reason,
                OccurredAtUtc = DateTime.UtcNow,
                Detail = $"Reason={reason}"
            });
        }

        public static IReadOnlyDictionary<string, long> Snapshot()
        {
            return RejectCounts
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        }
    }
}
