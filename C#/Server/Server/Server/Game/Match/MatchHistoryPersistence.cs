using Google.Protobuf.Protocol;
using Microsoft.EntityFrameworkCore;
using Server.DB;
using Server.Monitoring;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Server.Game.Match
{
    public static class MatchHistoryPersistence
    {
        private const int MaxQueuedEvents = 1024;
        private static readonly BlockingCollection<MatchHistoryEvent> Events = new BlockingCollection<MatchHistoryEvent>(MaxQueuedEvents);
        private static readonly Task Worker;

        static MatchHistoryPersistence()
        {
            Worker = Task.Run(ProcessEvents);
        }

        public static void RecordMatched(int partyId, string queueKey, RoomType targetRoomType, IReadOnlyList<MatchHistoryMemberSnapshot> members, DateTime createdAtUtc)
        {
            RecentEventBuffer.Add(new RecentEventSnapshot
            {
                Type = "PartyMatched",
                PartyId = partyId,
                QueueKey = queueKey,
                RoomType = targetRoomType.ToString(),
                Count = members?.Count ?? 0,
                OccurredAtUtc = createdAtUtc,
                Detail = $"PartyId={partyId}, Members={members?.Count ?? 0}, QueueKey={queueKey}"
            });

            Enqueue(new MatchCreatedEvent
            {
                PartyId = partyId,
                QueueKey = queueKey,
                TargetRoomType = targetRoomType.ToString(),
                Members = members?.ToList() ?? new List<MatchHistoryMemberSnapshot>(),
                CreatedAtUtc = createdAtUtc
            });
        }

        public static void RecordTransferStarted(int partyId, RoomType targetRoomType, int targetRoomId, int transferId, DateTime startedAtUtc)
        {
            if (partyId <= 0)
                return;

            RecentEventBuffer.Add(new RecentEventSnapshot
            {
                Type = "TransferStarted",
                PartyId = partyId,
                RoomType = targetRoomType.ToString(),
                RoomId = targetRoomId,
                TransferId = transferId,
                OccurredAtUtc = startedAtUtc,
                Detail = $"PartyId={partyId}, RoomId={targetRoomId}, TransferId={transferId}"
            });

            Enqueue(new TransferStartedEvent
            {
                PartyId = partyId,
                TargetRoomType = targetRoomType.ToString(),
                TargetRoomId = targetRoomId,
                TransferId = transferId,
                StartedAtUtc = startedAtUtc
            });
        }

        public static void RecordDungeonEntered(int partyId, int targetRoomId, int transferId, DateTime enteredAtUtc)
        {
            if (partyId <= 0)
                return;

            RecentEventBuffer.Add(new RecentEventSnapshot
            {
                Type = "DungeonEntered",
                PartyId = partyId,
                RoomId = targetRoomId,
                TransferId = transferId,
                OccurredAtUtc = enteredAtUtc,
                Detail = $"PartyId={partyId}, RoomId={targetRoomId}, TransferId={transferId}"
            });

            Enqueue(new DungeonEnteredEvent
            {
                PartyId = partyId,
                TargetRoomId = targetRoomId,
                TransferId = transferId,
                EnteredAtUtc = enteredAtUtc
            });
        }

        public static void RecordFailed(int partyId, string failureReason)
        {
            if (partyId <= 0)
                return;

            RecentEventBuffer.Add(new RecentEventSnapshot
            {
                Type = "MatchRejected",
                Reason = failureReason ?? "Unknown",
                PartyId = partyId,
                OccurredAtUtc = DateTime.UtcNow,
                Detail = $"PartyId={partyId}, Reason={failureReason ?? "Unknown"}"
            });

            Enqueue(new MatchFailedEvent
            {
                PartyId = partyId,
                FailureReason = failureReason ?? "Unknown"
            });
        }

        private static void Enqueue(MatchHistoryEvent item)
        {
            if (item == null)
                return;

            if (string.Equals(Environment.GetEnvironmentVariable("PROJECT_DAWN_MATCH_HISTORY_DISABLED"), "true", StringComparison.OrdinalIgnoreCase))
                return;

            if (Events.TryAdd(item) == false)
            {
                Console.WriteLine($"[MATCH_HISTORY][WARN] Event dropped. Reason=QueueFull, Event={item.EventName}, PartyId={item.PartyId}, QueueCount={Events.Count}, MaxQueuedEvents={MaxQueuedEvents}");
                return;
            }

            Console.WriteLine($"[MATCH_HISTORY] Event queued. Event={item.EventName}, PartyId={item.PartyId}, QueueCount={Events.Count}, MaxQueuedEvents={MaxQueuedEvents}");
        }

        private static async Task ProcessEvents()
        {
            foreach (MatchHistoryEvent item in Events.GetConsumingEnumerable())
            {
                try
                {
                    if (string.Equals(Environment.GetEnvironmentVariable("PROJECT_DAWN_MATCH_HISTORY_FAIL_WRITES"), "true", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("PROJECT_DAWN_MATCH_HISTORY_FAIL_WRITES=true");

                    await item.ApplyAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[MATCH_HISTORY][WARN] DB write failed. Event={item.EventName}, PartyId={item.PartyId}, Reason={ex.Message}");
                }
            }
        }

        private static AppDbContext CreateDbContext()
        {
            string connectionString = Environment.GetEnvironmentVariable("PROJECT_DAWN_MATCH_HISTORY_CONNECTION_STRING");
            return string.IsNullOrWhiteSpace(connectionString)
                ? new AppDbContext()
                : new AppDbContext(connectionString);
        }

        private abstract class MatchHistoryEvent
        {
            public int PartyId { get; set; }
            public abstract string EventName { get; }
            public abstract Task ApplyAsync();

            protected async Task<MatchHistoryDb> FindHistoryAsync(AppDbContext db)
            {
                return await db.MatchHistories.FirstOrDefaultAsync(h => h.PartyId == PartyId).ConfigureAwait(false);
            }
        }

        private sealed class MatchCreatedEvent : MatchHistoryEvent
        {
            public override string EventName => "Matched";
            public string QueueKey { get; set; }
            public string TargetRoomType { get; set; }
            public List<MatchHistoryMemberSnapshot> Members { get; set; }
            public DateTime CreatedAtUtc { get; set; }

            public override async Task ApplyAsync()
            {
                using (AppDbContext db = CreateDbContext())
                {
                    MatchHistoryDb existing = await FindHistoryAsync(db).ConfigureAwait(false);
                    if (existing != null)
                        return;

                    MatchHistoryDb history = new MatchHistoryDb
                    {
                        PartyId = PartyId,
                        QueueKey = QueueKey,
                        TargetRoomType = TargetRoomType,
                        CreatedAtUtc = CreatedAtUtc,
                        ResultStatus = "Pending",
                        Members = Members.Select(member => new MatchHistoryMemberDb
                        {
                            PlayerId = member.PlayerId,
                            PlayerName = member.PlayerName
                        }).ToList()
                    };

                    db.MatchHistories.Add(history);
                    await db.SaveChangesAsync().ConfigureAwait(false);
                    Console.WriteLine($"[MATCH_HISTORY] Created. PartyId={PartyId}, MatchHistoryId={history.Id}, Members={Members.Count}, QueueKey={QueueKey}");
                }
            }
        }

        private sealed class TransferStartedEvent : MatchHistoryEvent
        {
            public override string EventName => "TransferStarted";
            public string TargetRoomType { get; set; }
            public int TargetRoomId { get; set; }
            public int TransferId { get; set; }
            public DateTime StartedAtUtc { get; set; }

            public override async Task ApplyAsync()
            {
                using (AppDbContext db = CreateDbContext())
                {
                    MatchHistoryDb history = await FindHistoryAsync(db).ConfigureAwait(false);
                    if (history == null)
                    {
                        Console.WriteLine($"[MATCH_HISTORY][WARN] Transfer start skipped. Reason=HistoryNotFound, PartyId={PartyId}, TargetRoomId={TargetRoomId}, TransferId={TransferId}");
                        return;
                    }

                    history.TargetRoomType = TargetRoomType;
                    history.TargetRoomId = TargetRoomId;
                    history.TransferId = TransferId;
                    history.TransferStartedAtUtc = StartedAtUtc;
                    await db.SaveChangesAsync().ConfigureAwait(false);
                    Console.WriteLine($"[MATCH_HISTORY] TransferStarted. PartyId={PartyId}, MatchHistoryId={history.Id}, TargetRoomId={TargetRoomId}, TransferId={TransferId}");
                }
            }
        }

        private sealed class DungeonEnteredEvent : MatchHistoryEvent
        {
            public override string EventName => "DungeonEntered";
            public int TargetRoomId { get; set; }
            public int TransferId { get; set; }
            public DateTime EnteredAtUtc { get; set; }

            public override async Task ApplyAsync()
            {
                using (AppDbContext db = CreateDbContext())
                {
                    MatchHistoryDb history = await FindHistoryAsync(db).ConfigureAwait(false);
                    if (history == null)
                    {
                        Console.WriteLine($"[MATCH_HISTORY][WARN] Dungeon enter skipped. Reason=HistoryNotFound, PartyId={PartyId}, TargetRoomId={TargetRoomId}, TransferId={TransferId}");
                        return;
                    }

                    history.TargetRoomId = TargetRoomId;
                    history.TransferId = TransferId;
                    history.DungeonEnteredAtUtc = EnteredAtUtc;
                    history.ResultStatus = "Entered";
                    history.FailureReason = null;
                    await db.SaveChangesAsync().ConfigureAwait(false);
                    Console.WriteLine($"[MATCH_HISTORY] DungeonEntered. PartyId={PartyId}, MatchHistoryId={history.Id}, TargetRoomId={TargetRoomId}, TransferId={TransferId}");
                }
            }
        }

        private sealed class MatchFailedEvent : MatchHistoryEvent
        {
            public override string EventName => "Failed";
            public string FailureReason { get; set; }

            public override async Task ApplyAsync()
            {
                using (AppDbContext db = CreateDbContext())
                {
                    MatchHistoryDb history = await FindHistoryAsync(db).ConfigureAwait(false);
                    if (history == null)
                    {
                        Console.WriteLine($"[MATCH_HISTORY][WARN] Failure skipped. Reason=HistoryNotFound, PartyId={PartyId}, FailureReason={FailureReason}");
                        return;
                    }

                    if (history.ResultStatus == "Entered")
                        return;

                    history.ResultStatus = "Failed";
                    history.FailureReason = FailureReason;
                    await db.SaveChangesAsync().ConfigureAwait(false);
                    Console.WriteLine($"[MATCH_HISTORY] Failed. PartyId={PartyId}, MatchHistoryId={history.Id}, FailureReason={FailureReason}");
                }
            }
        }
    }

    public sealed class MatchHistoryMemberSnapshot
    {
        public int PlayerId { get; set; }
        public string PlayerName { get; set; }
    }
}


