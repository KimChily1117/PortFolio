using Google.Protobuf.Protocol;
using Server.Game.Room;
using Server.Monitoring;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Server.Game.Match
{
    public class MatchManager
    {
        public static MatchManager Instance { get; } = new MatchManager();

        private const int RequiredPartySize = 4;
        private const int MaxPartySize = 4;
        private static readonly TimeSpan MatchTicketLifetime = TimeSpan.FromMinutes(5);

        private readonly object _lock = new object();
        private readonly IMatchQueueStore _queueStore = new InMemoryMatchQueueStore();
        private readonly Dictionary<int, MatchParty> _partiesByPlayerId = new Dictionary<int, MatchParty>();
        private int _partyId = 1;

        private MatchManager()
        {
        }

        public void RequestMatch(ClientSession session)
        {
            if (IsValidSession(session) == false)
            {
                Console.WriteLine($"[MATCH] Request rejected. Reason=InvalidSession, SessionId={session?.SessionId ?? 0}");
                RecordMatchRejected("InvalidSession", null, null, null);
                return;
            }

            if (session.IsTransferring)
            {
                Console.WriteLine($"[MATCH] Request rejected. Reason=Transferring, SessionId={session.SessionId}, PendingRoomId={session.PendingRoomId}");
                RecordMatchRejected("Transferring", session.MyPlayer?.Id, session.MyPlayer?.Info?.Name, null);
                return;
            }

            MatchProfile profile = null;
            string rejectReason = null;
            if (MatchProfileProvider.TryBuild(session, MatchMode.Bakal, RoomType.Bakal, out profile, out rejectReason) == false)
            {
                if (profile != null && rejectReason == "MissingRequiredEquipment")
                {
                    Console.WriteLine($"[MATCH] Request rejected. Reason={rejectReason}, PlayerId={profile.PlayerId}, PlayerName={profile.PlayerName}, HasWeapon={profile.HasEquippedWeapon}, HasArmor={profile.HasEquippedArmor}");
                }
                else
                {
                    Console.WriteLine($"[MATCH] Request rejected. Reason={rejectReason ?? "InvalidMatchProfile"}, SessionId={session.SessionId}");
                }

                RecordMatchRejected(rejectReason ?? "InvalidMatchProfile", profile?.PlayerId, profile?.PlayerName, profile?.QueueKey?.ToString());
                SendCreateRoomReject(session, rejectReason ?? "InvalidMatchProfile", profile?.PlayerName);
                return;
            }

            MatchTicket ticket = CreateTicket(session, profile);
            MatchParty matchedParty = null;

            lock (_lock)
            {
                if (_partiesByPlayerId.ContainsKey(ticket.PlayerId))
                {
                    Console.WriteLine($"[MATCH] Request ignored. Reason=AlreadyInParty, SessionId={session.SessionId}, PlayerId={ticket.PlayerId}");
                    RecordMatchRejected("AlreadyInParty", ticket.PlayerId, ticket.PlayerName, ticket.QueueKey?.ToString());
                    return;
                }
            }

            _queueStore.CleanupExpired(DateTime.UtcNow);

            if (_queueStore.ContainsSession(ticket.SessionId) || _queueStore.ContainsPlayer(ticket.PlayerId))
            {
                Console.WriteLine($"[MATCH] Request ignored. Reason=AlreadyWaiting, SessionId={session.SessionId}, PlayerId={ticket.PlayerId}");
                RecordMatchRejected("AlreadyWaiting", ticket.PlayerId, ticket.PlayerName, ticket.QueueKey?.ToString());
                return;
            }

            if (_queueStore.Enqueue(ticket) == false)
            {
                Console.WriteLine($"[MATCH] Request ignored. Reason=AlreadyWaiting, SessionId={session.SessionId}, PlayerId={ticket.PlayerId}");
                RecordMatchRejected("AlreadyWaiting", ticket.PlayerId, ticket.PlayerName, ticket.QueueKey?.ToString());
                return;
            }

            Console.WriteLine($"[MATCH] Ticket created. Player={ticket.PlayerName}, PlayerId={ticket.PlayerId}, Level={ticket.Level}, LevelBucket={ticket.LevelBucket}, Mmr={ticket.Mmr}, MmrBucket={ticket.MmrBucket}, HasWeapon={ticket.HasEquippedWeapon}, HasArmor={ticket.HasEquippedArmor}, QueueKey={ticket.QueueKey}");
            RecentEventBuffer.Add(new RecentEventSnapshot
            {
                Type = "MatchAccepted",
                PlayerId = ticket.PlayerId,
                PlayerName = ticket.PlayerName,
                QueueKey = ticket.QueueKey?.ToString(),
                RoomType = ticket.TargetRoomType.ToString(),
                OccurredAtUtc = ticket.CreatedAt,
                Detail = $"Player={ticket.PlayerName}, QueueKey={ticket.QueueKey}"
            });

            SendCreateRoom(session);

            matchedParty = TryCreateMatchedParty(ticket.QueueKey);
            if (matchedParty == null)
            {
                Console.WriteLine($"[MATCH] Waiting for party member. SessionId={session.SessionId}, PlayerId={ticket.PlayerId}, PlayerName={ticket.PlayerName}, QueueKey={ticket.QueueKey}, QueueCount={_queueStore.Count(ticket.QueueKey)}/{RequiredPartySize}");
                return;
            }

            AutoStartPartyTransfer(matchedParty);
        }

        public void StartPartyDungeonTransfer(ClientSession session)
        {
            if (IsValidSession(session) == false)
            {
                Console.WriteLine($"[MATCH] Start rejected. Reason=InvalidSession, SessionId={session?.SessionId ?? 0}");
                return;
            }

            MatchParty party = null;
            lock (_lock)
            {
                _partiesByPlayerId.TryGetValue(session.MyPlayer.Id, out party);
            }

            if (party == null)
            {
                if (_queueStore.ContainsPlayer(session.MyPlayer.Id))
                {
                    Console.WriteLine($"[MATCH] Start rejected. Reason=WaitingForPartyMember, SessionId={session.SessionId}, PlayerId={session.MyPlayer.Id}");
                    return;
                }

                Console.WriteLine($"[MATCH] Start rejected. Reason=PartyNotFound, SessionId={session.SessionId}, PlayerId={session.MyPlayer.Id}");
                return;
            }

            lock (_lock)
            {
                if (party.IsInvalid)
                {
                    Console.WriteLine($"[MATCH] Start rejected. Reason=PartyInvalid, PartyId={party.PartyId}");
                    MatchHistoryPersistence.RecordFailed(party.PartyId, "PartyInvalid");
                    return;
                }

                if (party.Leader != session)
                {
                    Console.WriteLine($"[MATCH] Start rejected. Reason=NotLeader, SessionId={session.SessionId}, LeaderSessionId={party.Leader?.SessionId ?? 0}, PartyId={party.PartyId}");
                    return;
                }

                if (party.IsTransferStarted)
                {
                    Console.WriteLine($"[MATCH] Start rejected. Reason=TransferAlreadyStarted, PartyId={party.PartyId}");
                    return;
                }

                if (party.Members.Count < RequiredPartySize)
                {
                    Console.WriteLine($"[MATCH] Start rejected. Reason=PartyNotFull, PartyId={party.PartyId}, Count={party.Members.Count}");
                    return;
                }

                if (party.Members.Any(IsValidSession) == false || party.Members.Count(IsValidSession) != party.Members.Count)
                {
                    Console.WriteLine($"[MATCH] Start rejected. Reason=InvalidMember, PartyId={party.PartyId}");
                    MatchHistoryPersistence.RecordFailed(party.PartyId, "InvalidMember");
                    return;
                }

                ClientSession transferringMember = party.Members.FirstOrDefault(member => member.IsTransferring);
                if (transferringMember != null)
                {
                    Console.WriteLine($"[MATCH] Start rejected. Reason=MemberTransferring, PartyId={party.PartyId}, SessionId={transferringMember.SessionId}");
                    MatchHistoryPersistence.RecordFailed(party.PartyId, "MemberTransferring");
                    return;
                }

                party.IsTransferStarted = true;
            }

            Console.WriteLine($"[MATCH] Start party transfer. PartyId={party.PartyId}, Target={RoomType.Bakal}, Members={FormatMembers(party.Members)}");
            RoomTransferService.Instance.StartPartyDungeonTransfer(party.Members, RoomType.Bakal, party.PartyId);
            lock (_lock)
            {
                RemovePartyMap(party);
            }
        }


        public MatchingQueuesSnapshot CreateQueueSnapshot()
        {
            return _queueStore.CreateSnapshot(DateTime.UtcNow, RequiredPartySize);
        }
        public void OnDisconnected(ClientSession session)
        {
            if (session == null || session.MyPlayer == null)
                return;

            MatchParty party = null;
            lock (_lock)
            {
                if (_partiesByPlayerId.TryGetValue(session.MyPlayer.Id, out party) == false)
                    return;

                party.IsInvalid = true;
                RemovePartyMap(party);
            }

            Console.WriteLine($"[MATCH] Party invalidated. Reason=MemberDisconnected, PartyId={party.PartyId}, SessionId={session.SessionId}");
            MatchHistoryPersistence.RecordFailed(party.PartyId, "MemberDisconnected");
        }

        private MatchParty TryCreateMatchedParty(MatchQueueKey queueKey)
        {
            while (_queueStore.Count(queueKey) >= RequiredPartySize)
            {
                IReadOnlyList<MatchTicket> tickets = _queueStore.TryDequeueBatch(queueKey, RequiredPartySize);
                if (tickets == null || tickets.Count == 0)
                    return null;

                List<ResolvedMatchTicket> resolvedTickets = new List<ResolvedMatchTicket>(RequiredPartySize);
                HashSet<int> selectedSessionIds = new HashSet<int>();
                HashSet<int> selectedPlayerIds = new HashSet<int>();

                foreach (MatchTicket ticket in tickets)
                {
                    if (TryResolveTicket(ticket, out ClientSession resolvedSession) == false)
                    {
                        Console.WriteLine($"[MATCH] Waiting ticket skipped. Reason=StaleTicket, TicketId={ticket?.TicketId ?? "null"}, SessionId={ticket?.SessionId ?? 0}, PlayerId={ticket?.PlayerId ?? 0}");
                        continue;
                    }

                    if (selectedSessionIds.Add(ticket.SessionId) == false || selectedPlayerIds.Add(ticket.PlayerId) == false)
                    {
                        Console.WriteLine($"[MATCH] Waiting ticket skipped. Reason=DuplicateInBatch, TicketId={ticket.TicketId}, SessionId={ticket.SessionId}, PlayerId={ticket.PlayerId}");
                        continue;
                    }

                    resolvedTickets.Add(new ResolvedMatchTicket(ticket, resolvedSession));
                }

                if (resolvedTickets.Count >= RequiredPartySize)
                {
                    List<ClientSession> matchedMembers = resolvedTickets
                        .Take(RequiredPartySize)
                        .Select(resolvedTicket => resolvedTicket.Session)
                        .ToList();

                    lock (_lock)
                    {
                        if (matchedMembers.Any(member => _partiesByPlayerId.ContainsKey(member.MyPlayer.Id)))
                        {
                            foreach (ResolvedMatchTicket resolvedTicket in resolvedTickets)
                            {
                                _queueStore.Enqueue(resolvedTicket.Ticket);
                            }

                            return null;
                        }

                        foreach (ResolvedMatchTicket resolvedTicket in resolvedTickets)
                        {
                            resolvedTicket.Ticket.State = MatchTicketState.Matched;
                        }

                        return CreateParty(matchedMembers, queueKey);
                    }
                }

                foreach (ResolvedMatchTicket resolvedTicket in resolvedTickets)
                {
                    _queueStore.Enqueue(resolvedTicket.Ticket);
                }
            }

            return null;
        }

        private MatchParty CreateParty(IReadOnlyList<ClientSession> members, MatchQueueKey queueKey)
        {
            if (members == null || members.Count == 0)
                return null;

            MatchParty party = new MatchParty();
            party.PartyId = _partyId++;
            party.Leader = members[0];
            party.CreatedAt = DateTime.UtcNow;
            party.QueueKey = queueKey;

            int memberCount = Math.Min(members.Count, MaxPartySize);
            for (int i = 0; i < memberCount; i++)
            {
                ClientSession member = members[i];
                member.MyPlayer.Info.IsMaster = i == 0;
                party.Members.Add(member);
                _partiesByPlayerId[member.MyPlayer.Id] = party;
            }

            SendEnterParty(party);
            Console.WriteLine($"[MATCH] Party matched. PartyId={party.PartyId}, Leader={party.Leader.MyPlayer.Info.Name}, Count={party.Members.Count}, Members={FormatMembers(party.Members)}");
            MatchHistoryPersistence.RecordMatched(party.PartyId, party.QueueKey?.ToString(), RoomType.Bakal, BuildMemberSnapshots(party.Members), party.CreatedAt);
            return party;
        }

        private void AutoStartPartyTransfer(MatchParty party)
        {
            if (party == null)
                return;

            lock (_lock)
            {
                if (party.IsInvalid)
                {
                    Console.WriteLine($"[MATCH] Auto start rejected. Reason=PartyInvalid, PartyId={party.PartyId}");
                    MatchHistoryPersistence.RecordFailed(party.PartyId, "PartyInvalid");
                    return;
                }

                if (party.IsTransferStarted)
                {
                    Console.WriteLine($"[MATCH] Auto start rejected. Reason=TransferAlreadyStarted, PartyId={party.PartyId}");
                    return;
                }

                if (party.Members.Count < RequiredPartySize)
                {
                    Console.WriteLine($"[MATCH] Auto start rejected. Reason=PartyNotFull, PartyId={party.PartyId}, Count={party.Members.Count}");
                    return;
                }

                if (party.Members.Any(IsValidSession) == false || party.Members.Count(IsValidSession) != party.Members.Count)
                {
                    Console.WriteLine($"[MATCH] Auto start rejected. Reason=InvalidMember, PartyId={party.PartyId}");
                    MatchHistoryPersistence.RecordFailed(party.PartyId, "InvalidMember");
                    return;
                }

                ClientSession transferringMember = party.Members.FirstOrDefault(member => member.IsTransferring);
                if (transferringMember != null)
                {
                    Console.WriteLine($"[MATCH] Auto start rejected. Reason=MemberTransferring, PartyId={party.PartyId}, SessionId={transferringMember.SessionId}");
                    MatchHistoryPersistence.RecordFailed(party.PartyId, "MemberTransferring");
                    return;
                }

                party.IsTransferStarted = true;
            }

            Console.WriteLine($"[MATCH] Auto start party transfer. PartyId={party.PartyId}, Members={FormatMembers(party.Members)}");
            RoomTransferService.Instance.StartPartyDungeonTransfer(party.Members, RoomType.Bakal, party.PartyId);

            lock (_lock)
            {
                RemovePartyMap(party);
            }
        }

        private void SendCreateRoom(ClientSession session)
        {
            session.MyPlayer.Info.IsMaster = true;

            S_CreateRoom createRoom = new S_CreateRoom();
            createRoom.Playerinfo = session.MyPlayer.Info;
            createRoom.ResponseCode = 1;
            session.Send(createRoom);
        }

        private void SendCreateRoomReject(ClientSession session, string reason, string playerName = null)
        {
            S_CreateRoom createRoom = new S_CreateRoom();
            if (session?.MyPlayer?.Info != null)
                createRoom.Playerinfo = session.MyPlayer.Info;

            createRoom.ResponseCode = 0;
            session.Send(createRoom);

            Console.WriteLine($"[MATCH] S_CreateRoom reject sent. Reason={reason}, SessionId={session?.SessionId ?? 0}, PlayerName={playerName ?? session?.MyPlayer?.Info?.Name ?? "Unknown"}");
        }

        private void SendEnterParty(MatchParty party)
        {
            S_EnterParty enterParty = new S_EnterParty();
            enterParty.Playerinfo = party.Leader.MyPlayer.Info;
            enterParty.ResponseCode = 1;

            foreach (ClientSession member in party.Members)
            {
                LobbyPlayerInfo lobbyInfo = new LobbyPlayerInfo();
                lobbyInfo.Name = member.MyPlayer.Info.Name;
                enterParty.PartyMembers.Add(lobbyInfo);
            }

            foreach (ClientSession member in party.Members)
            {
                member.Send(enterParty);
            }
        }

        private MatchTicket CreateTicket(ClientSession session, MatchProfile profile)
        {
            DateTime now = DateTime.UtcNow;
            return new MatchTicket()
            {
                TicketId = Guid.NewGuid().ToString("N"),
                SessionId = session.SessionId,
                PlayerId = profile.PlayerId,
                PlayerName = profile.PlayerName,
                Mode = profile.Mode,
                TargetRoomType = profile.TargetRoomType,
                Level = profile.Level,
                LevelBucket = profile.LevelBucket,
                Mmr = profile.Mmr,
                MmrBucket = profile.MmrBucket,
                HasEquippedWeapon = profile.HasEquippedWeapon,
                HasEquippedArmor = profile.HasEquippedArmor,
                QueueKey = profile.QueueKey,
                CreatedAt = now,
                ExpiresAt = now.Add(MatchTicketLifetime),
                State = MatchTicketState.Waiting
            };
        }

        private bool TryResolveTicket(MatchTicket ticket, out ClientSession session)
        {
            session = null;

            if (ticket == null)
                return false;

            if (ticket.ExpiresAt <= DateTime.UtcNow)
                return false;

            session = SessionManager.Instance.Find(ticket.SessionId);
            if (IsValidSession(session) == false)
                return false;

            if (session.IsTransferring)
                return false;

            if (session.MyPlayer.Id != ticket.PlayerId)
                return false;

            lock (_lock)
            {
                return _partiesByPlayerId.ContainsKey(ticket.PlayerId) == false;
            }
        }

        private void RemovePartyMap(MatchParty party)
        {
            if (party == null)
                return;

            foreach (ClientSession member in party.Members)
            {
                if (member?.MyPlayer == null)
                    continue;

                _partiesByPlayerId.Remove(member.MyPlayer.Id);
            }
        }

        private bool IsValidSession(ClientSession session)
        {
            return session != null &&
                   session.MyPlayer != null &&
                   session.MyPlayer.Info != null;
        }

        private IReadOnlyList<MatchHistoryMemberSnapshot> BuildMemberSnapshots(IReadOnlyList<ClientSession> members)
        {
            return members
                .Where(member => member?.MyPlayer?.Info != null)
                .Select(member => new MatchHistoryMemberSnapshot
                {
                    PlayerId = member.MyPlayer.Id,
                    PlayerName = member.MyPlayer.Info.Name
                })
                .ToList();
        }
        private static void RecordMatchRejected(string reason, int? playerId, string playerName, string queueKey)
        {
            RecentEventBuffer.Add(new RecentEventSnapshot
            {
                Type = "MatchRejected",
                Reason = reason ?? "Unknown",
                PlayerId = playerId,
                PlayerName = playerName,
                QueueKey = queueKey,
                OccurredAtUtc = DateTime.UtcNow,
                Detail = $"Reason={reason ?? "Unknown"}, Player={playerName ?? "Unknown"}"
            });
        }

        private string FormatMembers(IReadOnlyList<ClientSession> members)
        {
            return string.Join(",", members.Select(s => s?.MyPlayer?.Info?.Name ?? "null"));
        }

        private class ResolvedMatchTicket
        {
            public ResolvedMatchTicket(MatchTicket ticket, ClientSession session)
            {
                Ticket = ticket;
                Session = session;
            }

            public MatchTicket Ticket { get; }
            public ClientSession Session { get; }
        }

        private class MatchParty
        {
            public int PartyId;
            public ClientSession Leader;
            public bool IsTransferStarted;
            public bool IsInvalid;
            public DateTime CreatedAt;
            public MatchQueueKey QueueKey;
            public List<ClientSession> Members { get; } = new List<ClientSession>();
        }
    }
}





