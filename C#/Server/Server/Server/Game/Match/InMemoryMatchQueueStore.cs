using System;
using System.Collections.Generic;
using System.Linq;
using Server.Monitoring;

namespace Server.Game.Match
{
    public class InMemoryMatchQueueStore : IMatchQueueStore
    {
        private readonly object _lock = new object();
        private readonly Dictionary<string, MatchTicket> _ticketsById = new Dictionary<string, MatchTicket>();
        private readonly Dictionary<int, string> _ticketIdsBySessionId = new Dictionary<int, string>();
        private readonly Dictionary<int, string> _ticketIdsByPlayerId = new Dictionary<int, string>();
        private readonly Dictionary<MatchQueueKey, Queue<string>> _queuesByKey = new Dictionary<MatchQueueKey, Queue<string>>();

        public bool Enqueue(MatchTicket ticket)
        {
            if (ticket == null || string.IsNullOrEmpty(ticket.TicketId) || ticket.QueueKey == null)
                return false;

            lock (_lock)
            {
                if (_ticketsById.ContainsKey(ticket.TicketId))
                    return false;

                if (_ticketIdsBySessionId.ContainsKey(ticket.SessionId))
                    return false;

                if (_ticketIdsByPlayerId.ContainsKey(ticket.PlayerId))
                    return false;

                ticket.State = MatchTicketState.Waiting;
                _ticketsById[ticket.TicketId] = ticket;
                _ticketIdsBySessionId[ticket.SessionId] = ticket.TicketId;
                _ticketIdsByPlayerId[ticket.PlayerId] = ticket.TicketId;
                GetQueue(ticket.QueueKey).Enqueue(ticket.TicketId);
                return true;
            }
        }

        public IReadOnlyList<MatchTicket> TryDequeueBatch(MatchQueueKey queueKey, int count)
        {
            if (count <= 0 || queueKey == null)
                return Array.Empty<MatchTicket>();

            lock (_lock)
            {
                Queue<string> queue = GetQueue(queueKey);
                if (CountLocked(queueKey) < count)
                    return Array.Empty<MatchTicket>();

                List<MatchTicket> tickets = new List<MatchTicket>(count);
                while (queue.Count > 0 && tickets.Count < count)
                {
                    string ticketId = queue.Dequeue();
                    if (_ticketsById.TryGetValue(ticketId, out MatchTicket ticket) == false)
                        continue;

                    RemoveLocked(ticket);
                    tickets.Add(ticket);
                }

                return tickets;
            }
        }

        public bool Remove(string ticketId)
        {
            if (string.IsNullOrEmpty(ticketId))
                return false;

            lock (_lock)
            {
                if (_ticketsById.TryGetValue(ticketId, out MatchTicket ticket) == false)
                    return false;

                RemoveLocked(ticket);
                return true;
            }
        }

        public bool ContainsSession(int sessionId)
        {
            lock (_lock)
            {
                return _ticketIdsBySessionId.ContainsKey(sessionId);
            }
        }

        public bool ContainsPlayer(int playerId)
        {
            lock (_lock)
            {
                return _ticketIdsByPlayerId.ContainsKey(playerId);
            }
        }

        public int Count(MatchQueueKey queueKey)
        {
            if (queueKey == null)
                return 0;

            lock (_lock)
            {
                return CountLocked(queueKey);
            }
        }

        public int CleanupExpired(DateTime now)
        {
            lock (_lock)
            {
                List<string> expiredTicketIds = new List<string>();
                foreach (MatchTicket ticket in _ticketsById.Values)
                {
                    if (ticket.ExpiresAt <= now)
                        expiredTicketIds.Add(ticket.TicketId);
                }

                foreach (string ticketId in expiredTicketIds)
                {
                    if (_ticketsById.TryGetValue(ticketId, out MatchTicket ticket))
                    {
                        ticket.State = MatchTicketState.Expired;
                        RemoveLocked(ticket);
                    }
                }

                return expiredTicketIds.Count;
            }
        }


        public MatchingQueuesSnapshot CreateSnapshot(DateTime nowUtc, int partySize)
        {
            lock (_lock)
            {
                MatchingQueuesSnapshot snapshot = new MatchingQueuesSnapshot
                {
                    SnapshotUpdatedAtUtc = nowUtc
                };

                foreach (KeyValuePair<MatchQueueKey, Queue<string>> pair in _queuesByKey)
                {
                    List<MatchTicket> waitingTickets = new List<MatchTicket>();
                    foreach (string ticketId in pair.Value)
                    {
                        if (_ticketsById.TryGetValue(ticketId, out MatchTicket ticket) == false)
                            continue;

                        if (ticket.State != MatchTicketState.Waiting || ticket.ExpiresAt <= nowUtc)
                            continue;

                        waitingTickets.Add(ticket);
                    }

                    if (waitingTickets.Count == 0)
                        continue;

                    MatchingQueueSnapshot queueSnapshot = new MatchingQueueSnapshot
                    {
                        QueueKey = pair.Key?.ToString(),
                        DungeonType = pair.Key?.TargetRoomType.ToString(),
                        RoomType = pair.Key?.TargetRoomType.ToString(),
                        WaitingCount = waitingTickets.Count,
                        PartySize = partySize
                    };

                    foreach (MatchTicket ticket in waitingTickets.OrderBy(t => t.CreatedAt))
                    {
                        queueSnapshot.Players.Add(new MatchingQueuePlayerSnapshot
                        {
                            SessionId = ticket.SessionId,
                            PlayerDbId = ticket.PlayerId,
                            PlayerName = ticket.PlayerName ?? "Unknown",
                            Level = ticket.Level,
                            Mmr = ticket.Mmr,
                            HasWeapon = ticket.HasEquippedWeapon,
                            HasArmor = ticket.HasEquippedArmor,
                            WaitingSeconds = Math.Max(0, (nowUtc - ticket.CreatedAt).TotalSeconds)
                        });
                    }

                    snapshot.Queues.Add(queueSnapshot);
                }

                snapshot.QueueCount = snapshot.Queues.Count;
                snapshot.TotalWaitingPlayers = snapshot.Queues.Sum(q => q.WaitingCount);
                return snapshot;
            }
        }
        private Queue<string> GetQueue(MatchQueueKey queueKey)
        {
            if (_queuesByKey.TryGetValue(queueKey, out Queue<string> queue) == false)
            {
                queue = new Queue<string>();
                _queuesByKey[queueKey] = queue;
            }

            return queue;
        }

        private int CountLocked(MatchQueueKey queueKey)
        {
            int count = 0;
            Queue<string> queue = GetQueue(queueKey);
            foreach (string ticketId in queue)
            {
                if (_ticketsById.ContainsKey(ticketId))
                    count++;
            }

            return count;
        }

        private void RemoveLocked(MatchTicket ticket)
        {
            _ticketsById.Remove(ticket.TicketId);
            _ticketIdsBySessionId.Remove(ticket.SessionId);
            _ticketIdsByPlayerId.Remove(ticket.PlayerId);
        }
    }
}

