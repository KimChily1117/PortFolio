using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace DnfRaidDemo
{
    public enum NetworkRole { Offline, Host, Client }

    /// <summary>
    /// Two-player, host-authoritative UDP transport for this prototype.
    /// It is an original demo protocol; it does not contact or emulate DNF servers.
    /// Call every method on the Unity main thread.
    /// </summary>
    public sealed class RaidNetwork : IDisposable
    {
        // V3 snapshots include authoritative action identity and timing; older builds cannot join.
        private const int Version = 3;
        private const int PressCount = 5;
        private const string Magic = "DnfRaidDemo";
        private const double Timeout = 6.0;
        private const int MaxDatagram = 60000;
        private UdpClient socket;
        private IPEndPoint peer;
        private string clientToken = "";
        private string session = "";
        private double lastHeard, lastInputHeard, nextHello, nextInput, nextSnapshot, nextPing;
        private long inputSequence, lastRemoteSequence, snapshotSequence, lastSnapshotSequence;
        private Vector2 localMove, remoteMove;
        private bool localBasic, remoteBasic;
        private readonly int[] localPress = new int[PressCount];
        private readonly int[] remotePress = new int[PressCount];
        private readonly int[] consumedPress = new int[PressCount];
        private readonly int[] acknowledgedPress = new int[PressCount];
        private readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();

        public NetworkRole Role { get; private set; }
        public bool Connected { get; private set; }
        public string Status { get; private set; } = "오프라인";
        public float PingMs { get; private set; }
        public int LocalPlayerId { get { return Role == NetworkRole.Client ? 1 : 0; } }
        public PlayerCommand RemoteCommand { get; private set; }
        public int ReceivedSnapshots { get; private set; }
        public int SentInputPackets { get; private set; }
        private double Now { get { return clock.Elapsed.TotalSeconds; } }

        [Serializable]
        private sealed class Message
        {
            public string Magic;
            public int V;
            public string Kind;
            public string ClientToken;
            public string Session;
            public long Seq;
            public float X, Y;
            public bool Basic;
            public int[] Press;
            public int[] Ack;
            public double Stamp;
            public Snapshot State;
        }

        [Serializable]
        private sealed class Snapshot
        {
            public List<PlayerState> Players;
            public BossState Boss;
            public List<AttackZone> Zones;
            public List<CombatEvent> Events;
            public EncounterMode Mode;
            public float Elapsed;
            public float TimeLimit;
            public int TickIndex;
        }

        public void StartHost(int port)
        {
            Dispose();
            if (!ValidPort(port)) { Status = "포트는 1024–65535 범위여야 합니다."; return; }
            try
            {
                socket = new UdpClient(new IPEndPoint(IPAddress.Any, port));
                socket.Client.Blocking = false;
                Role = NetworkRole.Host;
                session = NewToken();
                Status = "호스트 대기 중 · UDP " + port;
            }
            catch (Exception e) when (e is SocketException || e is ObjectDisposedException)
            {
                Dispose();
                Status = "호스트 시작 실패: " + e.Message;
            }
        }

        public void StartClient(string host, int port)
        {
            Dispose();
            IPAddress address;
            if (!ValidPort(port) || !IPAddress.TryParse(host, out address)
                || address.AddressFamily != AddressFamily.InterNetwork)
            {
                Status = "IPv4 주소와 1024–65535 범위의 포트를 입력하세요.";
                return;
            }
            try
            {
                socket = new UdpClient(new IPEndPoint(IPAddress.Any, 0));
                socket.Client.Blocking = false;
                peer = new IPEndPoint(address, port);
                clientToken = NewToken();
                Role = NetworkRole.Client;
                Status = "호스트 연결 중 · " + peer;
                SendHello();
            }
            catch (Exception e) when (e is SocketException || e is ObjectDisposedException)
            {
                Dispose();
                Status = "연결 시작 실패: " + e.Message;
            }
        }

        public void Poll(RaidSimulation sim)
        {
            if (socket == null) return;
            for (int i = 0; i < 64; i++)
            {
                try
                {
                    if (socket.Available == 0) break;
                    var source = new IPEndPoint(IPAddress.Any, 0);
                    byte[] bytes = socket.Receive(ref source);
                    if (bytes.Length == 0 || bytes.Length > MaxDatagram) continue;
                    Message message;
                    try { message = JsonUtility.FromJson<Message>(Encoding.UTF8.GetString(bytes)); }
                    catch (ArgumentException) { continue; }
                    if (message == null || message.Magic != Magic || message.V != Version) continue;
                    Receive(message, source, sim);
                }
                catch (SocketException e)
                {
                    if (e.SocketErrorCode != SocketError.WouldBlock
                        && e.SocketErrorCode != SocketError.ConnectionReset)
                        Status = "네트워크 수신 오류: " + e.SocketErrorCode;
                    break;
                }
                catch (ObjectDisposedException) { break; }
            }

            double now = Now;
            if (Connected && now - lastHeard > Timeout)
            {
                Connected = false;
                remoteMove = Vector2.zero;
                remoteBasic = false;
                ResetCommands();
                if (Role == NetworkRole.Host)
                {
                    peer = null;
                    clientToken = "";
                    session = NewToken();
                    Status = "상대 연결이 끊겼습니다 · 다시 접속할 수 있습니다.";
                }
                else
                {
                    session = "";
                    clientToken = NewToken();
                    Status = "호스트 응답 없음 · 재연결 중";
                }
            }
            if (Role == NetworkRole.Client && !Connected && now >= nextHello) SendHello();
            if (Connected && now >= nextPing)
            {
                Send(new Message { Kind = "ping", Stamp = now }, peer);
                nextPing = now + 1.0;
            }
            if (Role == NetworkRole.Client && Connected && now >= nextInput) TransmitInput();
        }

        private void Receive(Message m, IPEndPoint source, RaidSimulation sim)
        {
            if (Role == NetworkRole.Host && m.Kind == "hello")
            {
                if (!ValidToken(m.ClientToken)) return;
                if (peer != null && (!SamePeer(source) || clientToken != m.ClientToken)) return;
                if (peer == null)
                {
                    peer = source;
                    clientToken = m.ClientToken;
                    ResetCommands();
                    snapshotSequence = 0;
                }
                Connected = true;
                lastHeard = Now;
                Status = "2인 연결됨 · 호스트";
                Send(new Message { Kind = "welcome", ClientToken = clientToken }, peer);
                return;
            }
            if (!SamePeer(source)) return;
            if (Role == NetworkRole.Client && m.Kind == "welcome")
            {
                if (m.ClientToken != clientToken || !ValidToken(m.Session)) return;
                if (session != m.Session)
                {
                    ResetCommands();
                    session = m.Session;
                    lastSnapshotSequence = 0;
                }
                Connected = true;
                lastHeard = Now;
                Status = "2인 연결됨 · 참가자";
                return;
            }
            if (!Connected || m.Session != session) return;
            lastHeard = Now;
            if (m.Kind == "ping")
            {
                Send(new Message { Kind = "pong", Stamp = m.Stamp }, peer);
                return;
            }
            if (m.Kind == "pong")
            {
                double delta = Now - m.Stamp;
                if (delta >= 0 && delta <= Timeout)
                {
                    float sample = (float)(delta * 1000);
                    PingMs = PingMs <= 0 ? sample : Mathf.Lerp(PingMs, sample, 0.25f);
                }
                return;
            }
            if (Role == NetworkRole.Host && m.Kind == "input")
            {
                if (m.Seq <= lastRemoteSequence || !Finite(m.X) || !Finite(m.Y)
                    || m.Press == null || m.Press.Length != PressCount) return;
                for (int i = 0; i < PressCount; i++)
                    if (m.Press[i] < 0 || m.Press[i] > 1000000000
                        || m.Press[i] < consumedPress[i] || m.Press[i] - consumedPress[i] > 64) return;
                lastRemoteSequence = m.Seq;
                remoteMove = Vector2.ClampMagnitude(new Vector2(m.X, m.Y), 1);
                remoteBasic = m.Basic;
                for (int i = 0; i < PressCount; i++) remotePress[i] = Math.Max(remotePress[i], m.Press[i]);
                lastInputHeard = Now;
                return;
            }
            if (Role == NetworkRole.Client && m.Kind == "snapshot")
            {
                if (m.Seq <= lastSnapshotSequence || !ValidSnapshot(m.State)) return;
                lastSnapshotSequence = m.Seq;
                ReceivedSnapshots++;
                if (m.Ack != null && m.Ack.Length == PressCount)
                    for (int i = 0; i < PressCount; i++)
                        acknowledgedPress[i] = Math.Max(acknowledgedPress[i], Math.Min(localPress[i], m.Ack[i]));
                sim.Players = m.State.Players;
                sim.Boss = m.State.Boss;
                sim.Zones = m.State.Zones;
                sim.Events = m.State.Events;
                sim.Mode = m.State.Mode;
                sim.Elapsed = m.State.Elapsed;
                sim.TimeLimit = m.State.TimeLimit;
                sim.TickIndex = m.State.TickIndex;
            }
        }

        public void SendInput(PlayerCommand command)
        {
            if (Role != NetworkRole.Client || command == null) return;
            localMove = Vector2.ClampMagnitude(command.Move, 1);
            localBasic = command.Basic;
            // Held basic attack is a state. Discrete skills and jump use cumulative counters.
            // Every packet repeats unacknowledged presses; ordering and consumption prevent duplicates.
            CountPress(0, command.ArcSlash);
            CountPress(1, command.FlameWave);
            CountPress(2, command.Dash);
            CountPress(3, command.Awakening);
            CountPress(4, command.Jump);
            if (Connected && Now >= nextInput) TransmitInput();
        }

        private void CountPress(int i, bool pressed)
        {
            if (pressed && Connected && localPress[i] - acknowledgedPress[i] < 32) localPress[i]++;
        }

        private void TransmitInput()
        {
            Send(new Message
            {
                Kind = "input", Seq = ++inputSequence, X = localMove.x, Y = localMove.y,
                Basic = localBasic, Press = localPress
            }, peer);
            SentInputPackets++;
            nextInput = Now + 1.0 / 30.0;
        }

        public PlayerCommand ConsumeRemoteInput()
        {
            var command = new PlayerCommand { PlayerId = 1 };
            if (Role == NetworkRole.Host && Connected)
            {
                if (Now - lastInputHeard < 0.35)
                {
                    command.Move = remoteMove;
                    command.Basic = remoteBasic;
                }
                command.ArcSlash = ConsumePress(0);
                command.FlameWave = ConsumePress(1);
                command.Dash = ConsumePress(2);
                command.Awakening = ConsumePress(3);
                command.Jump = ConsumePress(4);
            }
            RemoteCommand = command;
            return command;
        }

        private bool ConsumePress(int i)
        {
            if (consumedPress[i] >= remotePress[i]) return false;
            consumedPress[i]++;
            return true;
        }

        public void SendSnapshot(RaidSimulation sim)
        {
            if (Role != NetworkRole.Host || !Connected || Now < nextSnapshot) return;
            Send(new Message
            {
                Kind = "snapshot", Seq = ++snapshotSequence, Ack = consumedPress,
                State = new Snapshot
                {
                    Players = sim.Players, Boss = sim.Boss, Zones = sim.Zones, Events = sim.Events,
                    Mode = sim.Mode, Elapsed = sim.Elapsed, TimeLimit = sim.TimeLimit, TickIndex = sim.TickIndex
                }
            }, peer);
            nextSnapshot = Now + 0.05;
        }

        private void SendHello()
        {
            Send(new Message { Kind = "hello", ClientToken = clientToken }, peer);
            nextHello = Now + 0.5;
        }

        private void Send(Message m, IPEndPoint endpoint)
        {
            if (socket == null || endpoint == null) return;
            m.Magic = Magic;
            m.V = Version;
            m.Session = session;
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(m));
                if (bytes.Length > MaxDatagram) { Status = "동기화 데이터 크기 초과"; return; }
                socket.Send(bytes, bytes.Length, endpoint);
            }
            catch (SocketException e)
            {
                if (e.SocketErrorCode != SocketError.WouldBlock
                    && e.SocketErrorCode != SocketError.ConnectionReset)
                    Status = "네트워크 송신 오류: " + e.SocketErrorCode;
            }
            catch (ObjectDisposedException) { }
        }

        private static bool ValidSnapshot(Snapshot s)
        {
            if (!(s != null && s.Players != null && s.Players.Count == 2 && s.Boss != null
                && s.Zones != null && s.Zones.Count <= 256 && s.Events != null && s.Events.Count <= 512
                && Finite(s.Elapsed) && Finite(s.TimeLimit) && s.TickIndex >= 0)) return false;
            foreach (PlayerState player in s.Players)
                if (player == null || player.Cooldowns == null || player.Cooldowns.Length != 5
                    || !Finite(player.Position.x) || !Finite(player.Position.y)
                    || !Finite(player.Hp) || !Finite(player.MaxHp)
                    || !Finite(player.JumpHeight) || !Finite(player.JumpVelocity)
                    || !ValidPlayerAction(player)) return false;
            foreach (AttackZone zone in s.Zones)
                if (zone == null || !Finite(zone.Center.x) || !Finite(zone.Center.y)) return false;
            foreach (CombatEvent item in s.Events) if (item == null) return false;
            return Finite(s.Boss.Position.x) && Finite(s.Boss.Position.y)
                && Finite(s.Boss.Hp) && Finite(s.Boss.MaxHp)
                && s.Boss.ActionSequence >= 0
                && ValidActionTime(s.Boss.ActionElapsed, s.Boss.ActionDuration);
        }

        private static bool ValidPlayerAction(PlayerState player)
        {
            return Enum.IsDefined(typeof(SkillId), player.ActiveSkill)
                && player.ActionSequence >= 0 && player.BasicComboStep >= 0 && player.BasicComboStep <= 2
                && (player.ActionFacing == -1 || player.ActionFacing == 1)
                && ValidActionTime(player.ActionElapsed, player.ActionDuration)
                && NonnegativeFinite(player.LandingTime);
        }

        private static bool ValidActionTime(float elapsed, float duration)
        {
            return NonnegativeFinite(elapsed) && NonnegativeFinite(duration) && elapsed <= duration + .0001f;
        }

        private static bool NonnegativeFinite(float value) { return Finite(value) && value >= 0; }

        private bool SamePeer(IPEndPoint endpoint)
        {
            return peer != null && peer.Port == endpoint.Port && peer.Address.Equals(endpoint.Address);
        }
        private static bool ValidPort(int port) { return port >= 1024 && port <= 65535; }
        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        private static bool ValidToken(string token) { return token != null && token.Length == 32; }
        private static string NewToken() { return Guid.NewGuid().ToString("N"); }

        private void ResetCommands()
        {
            Array.Clear(localPress, 0, PressCount);
            Array.Clear(remotePress, 0, PressCount);
            Array.Clear(consumedPress, 0, PressCount);
            Array.Clear(acknowledgedPress, 0, PressCount);
            localMove = remoteMove = Vector2.zero;
            localBasic = remoteBasic = false;
            inputSequence = lastRemoteSequence = 0;
            RemoteCommand = new PlayerCommand { PlayerId = 1 };
        }

        public void Dispose()
        {
            if (socket != null) socket.Close();
            socket = null;
            peer = null;
            Connected = false;
            Role = NetworkRole.Offline;
            Status = "오프라인";
            PingMs = 0;
            clientToken = session = "";
            lastHeard = lastInputHeard = nextHello = nextInput = nextSnapshot = nextPing = 0;
            snapshotSequence = lastSnapshotSequence = 0;
            ReceivedSnapshots = SentInputPackets = 0;
            ResetCommands();
        }
    }
}


