using System;
using System.Collections.Generic;
using System.Text;

using System.Net;
using Server.Monitoring;
using System.Linq;

namespace Server
{
	class SessionManager
	{
		static SessionManager _session = new SessionManager();
		public static SessionManager Instance { get { return _session; } }

		int _sessionId = 0;
		Dictionary<int, ClientSession> _sessions = new Dictionary<int, ClientSession>();
		object _lock = new object();

		public ClientSession Generate()
		{
			lock (_lock)
			{
				int sessionId = ++_sessionId;

				ClientSession session = new ClientSession();
				session.SessionId = sessionId;
				_sessions.Add(sessionId, session);

				Console.WriteLine($"Connected : {sessionId}");

				return session;
			}
		}

		public ClientSession Find(int id)
		{
			lock (_lock)
			{
				ClientSession session = null;
				_sessions.TryGetValue(id, out session);
				return session;
			}
		}

		public ClientSession FindByUdpToken(string token)
		{
			if (string.IsNullOrEmpty(token))
				return null;

			lock (_lock)
			{
				foreach (ClientSession session in _sessions.Values)
				{
					if (session.UdpToken != token)
						continue;

					if (session.UdpTokenExpiresAt < DateTime.UtcNow)
						return null;

					return session;
				}

				return null;
			}
		}

		public ClientSession FindByUdpEndPoint(EndPoint endPoint)
		{
			if (endPoint == null)
				return null;

			lock (_lock)
			{
				foreach (ClientSession session in _sessions.Values)
				{
					if (session.UdpEndPoint == null)
						continue;

					if (session.UdpEndPoint.Equals(endPoint))
						return session;
				}

				return null;
			}
		}

		public bool TryBindUdpEndPoint(ClientSession session, EndPoint endPoint, out string reason)
		{
			reason = null;
			if (session == null || endPoint == null)
			{
				reason = "InvalidSessionOrEndPoint";
				return false;
			}

			lock (_lock)
			{
				if (_sessions.TryGetValue(session.SessionId, out ClientSession current) == false || current != session)
				{
					reason = "SessionNotFound";
					return false;
				}

				foreach (ClientSession other in _sessions.Values)
				{
					if (other == null || other == session || other.UdpEndPoint == null)
						continue;

					if (other.UdpEndPoint.Equals(endPoint))
					{
						reason = "EndPointAlreadyRegistered";
						return false;
					}
				}

				if (session.UdpEndPoint != null && session.UdpEndPoint.Equals(endPoint) == false)
				{
					reason = "SessionAlreadyRegisteredToDifferentEndPoint";
					return false;
				}

				session.UdpEndPoint = endPoint;
				session.LastUdpSeenAt = DateTime.UtcNow;
				session.UdpToken = null;
				session.UdpTokenExpiresAt = DateTime.MinValue;
				session.ResetUdpMoveSecurityState();
				return true;
			}
		}

		public bool HasPendingTransferToRoom(int roomId)
		{
			if (roomId <= 0)
				return false;

			lock (_lock)
			{
				return _sessions.Values.Any(session =>
					session != null &&
					session.IsTransferring &&
					session.PendingRoomId == roomId);
			}
		}
		public OnlinePlayersSnapshot CreateOnlinePlayersSnapshot()
		{
			List<ClientSession> sessions;
			lock (_lock)
			{
				sessions = _sessions.Values.ToList();
			}

			OnlinePlayersSnapshot snapshot = new OnlinePlayersSnapshot();
			snapshot.SnapshotUpdatedAtUtc = DateTime.UtcNow;

			foreach (ClientSession session in sessions)
			{
				snapshot.Players.Add(OnlinePlayerSnapshot.FromSession(session));
			}

			snapshot.Count = snapshot.Players.Count;
			return snapshot;
		}
		public void Remove(ClientSession session)
		{
			lock (_lock)
			{
				_sessions.Remove(session.SessionId);
			}
		}
	}
}


