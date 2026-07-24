using System;
using System.Collections.Generic;
using Server.Monitoring;

namespace Server.Game.Match
{
    public interface IMatchQueueStore
    {
        bool Enqueue(MatchTicket ticket);
        IReadOnlyList<MatchTicket> TryDequeueBatch(MatchQueueKey queueKey, int count);
        bool Remove(string ticketId);
        bool ContainsSession(int sessionId);
        bool ContainsPlayer(int playerId);
        int Count(MatchQueueKey queueKey);
        int CleanupExpired(DateTime now);
        MatchingQueuesSnapshot CreateSnapshot(DateTime nowUtc, int partySize);
    }
}

