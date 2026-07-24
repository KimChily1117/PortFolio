using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Server.Monitoring
{
    public static class RecentEventBuffer
    {
        // 300 keeps several demo runs visible while making memory growth fixed and predictable.
        public const int MaxEvents = 300;
        private static readonly ConcurrentQueue<RecentEventSnapshot> Events = new ConcurrentQueue<RecentEventSnapshot>();
        private static int _count;

        public static void Add(RecentEventSnapshot item)
        {
            if (item == null)
                return;

            if (item.OccurredAtUtc == default)
                item.OccurredAtUtc = DateTime.UtcNow;

            Events.Enqueue(item);
            int count = Interlocked.Increment(ref _count);

            while (count > MaxEvents && Events.TryDequeue(out _))
            {
                count = Interlocked.Decrement(ref _count);
            }
        }

        public static RecentEventsSnapshot Snapshot()
        {
            List<RecentEventSnapshot> events = Events
                .ToArray()
                .OrderByDescending(e => e.OccurredAtUtc)
                .ToList();

            return new RecentEventsSnapshot
            {
                SnapshotUpdatedAtUtc = DateTime.UtcNow,
                Count = events.Count,
                MaxEvents = MaxEvents,
                ActiveCastRecorded = CombatActivityMetrics.ActiveCastRecorded,
                RejectReasonCounts = CombatRejectMetrics.Snapshot(),
                Events = events
            };
        }
    }

    public sealed class RecentEventsSnapshot
    {
        public DateTime SnapshotUpdatedAtUtc { get; set; }
        public int Count { get; set; }
        public int MaxEvents { get; set; }
        public long ActiveCastRecorded { get; set; }
        public IReadOnlyDictionary<string, long> RejectReasonCounts { get; set; } = new Dictionary<string, long>();
        public List<RecentEventSnapshot> Events { get; set; } = new List<RecentEventSnapshot>();
    }

    public sealed class RecentEventSnapshot
    {
        public string Type { get; set; }
        public string Reason { get; set; }
        public DateTime OccurredAtUtc { get; set; }
        public string Detail { get; set; }
        public int? PartyId { get; set; }
        public int? PlayerId { get; set; }
        public string PlayerName { get; set; }
        public string QueueKey { get; set; }
        public string RoomType { get; set; }
        public int? RoomId { get; set; }
        public int? TransferId { get; set; }
        public int? Count { get; set; }
    }
}


