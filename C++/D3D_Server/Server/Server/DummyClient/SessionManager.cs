using System;
using System.Collections.Generic;
using System.Text;
using Google.Protobuf.Protocol;

namespace DummyClient
{
    class SessionManager
    {
        static SessionManager _session = new SessionManager();
        public static SessionManager Instance { get { return _session; } }

        List<ServerSession> _sessions = new List<ServerSession>();
        object _lock = new object();
        Random _rand = new Random();

        public void SendForEach()
        {
            lock (_lock)
            {
                for (int i = 0; i < _sessions.Count; i++)
                {
                    ServerSession session = _sessions[i];

                    C_Move movePacket = new C_Move
                    {
                        ObjectId = (ulong)i, // 리스트 인덱스를 objectId로 사용

                        TargetPos = new Vector3
                        {
                            X = _rand.Next(6, 124),
                            Y = 0,
                            Z = _rand.Next(6, 124)
                        },
                        CellPos = new Vector2Int
                        {
                            X = _rand.Next(6, 124),
                            Z = _rand.Next(6, 124)
                        }
                    };

                    session.Send(movePacket);
                }
            }
        }

        public ServerSession Generate()
        {
            lock (_lock)
            {
                ServerSession session = new ServerSession();
                _sessions.Add(session);
                return session;
            }
        }
    }
}
