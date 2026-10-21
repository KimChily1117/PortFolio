using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Google.Protobuf;
using Google.Protobuf.Protocol;
using ProjectDawn.Combat;
using ProjectDawn.Network;
using Server;
using Server.Data;
using Server.Game;
using Server.Game.Object;
using Server.Game.Room;
using Server.Udp;

internal static class Program
{
    private static readonly List<Peer> Peers = new List<Peer>();
    private static int _passed;
    private static int Main()
    {
        Environment.SetEnvironmentVariable("PROJECT_DAWN_MATCH_HISTORY_DISABLED", "true");
        try
        {
            DataManager.ItemDict.Add(1, new WeaponData { id = 1, itemType = ItemType.Weapon, damage = 10 });
            DataManager.ItemDict.Add(100, new ArmorData { id = 100, itemType = ItemType.Armor, defence = 2 });
            var town = RoomManager.Instance.Add(RoomType.Town);
            // Only account/inventory setup is a fixture. No login bypass is added to the server executable.
            for (int i = 0; i < 4; i++)
            {
                var peer = new Peer(i + 1); Peers.Add(peer);
                town.EnterRoom(peer.Session.MyPlayer);
                peer.Client.Negotiate();
            }
            Until(() => Peers.All(p => p.Messages.OfType<S_RaidHello>().Any(h => h.Version == 1)), "version negotiation");
            for (int i = 0; i < 3; i++) Peers[i].Send(new C_CreateRoom());
            RunFor(100);
            Check(Peers.All(p => !p.Messages.OfType<S_SceneMove>().Any()), "three players cannot start a four-player match");
            Peers[3].Send(new C_CreateRoom());
            Until(() => Peers.All(p => p.Messages.OfType<S_SceneMove>().Any()), "real matchmaking transfer");
            var moves = Peers.Select(p => p.Messages.OfType<S_SceneMove>().Single()).ToArray();
            Check(moves.Select(m => m.TargetRoomId).Distinct().Count() == 1 &&
                Peers.All(p => p.Messages.OfType<S_EnterParty>().Any(m => m.PartyMembers.Count == 4)),
                "four socket clients matched into the same real GameRoom");
            for (int i = 0; i < 3; i++) Peers[i].Ready(moves[i]);
            RunFor(100);
            Check(Peers.All(p => p.Client.Start == null), "combat waits for all four scene-ready acknowledgements");
            var invalidReady = new C_SceneReady { TargetRoomId = moves[3].TargetRoomId, TransferId = moves[3].TransferId + 1,
                SceneType = moves[3].SceneType, TargetRoomType = moves[3].TargetRoomType };
            Peers[3].Send(invalidReady); RunFor(50);
            Check(Peers.All(p => p.Client.Start == null), "forged scene-ready transfer token cannot start combat");
            Peers[3].Ready(moves[3]);
            Until(() => Peers.All(p => p.Client.Start != null), "raid start");
            Until(()=>Peers.All(p=>p.Client.ClockTrusted),"four server-measured clock leases");
            Check(Peers.All(p=>p.Client.ClockState.Version>=2 && p.Client.ClockState.MaxBackdateUs<=120000),"four measured clock mappings bounded by input deadline");
            var host = Peers.Single(p => p.Client.IsHost);
            var other = Peers.First(p => p != host);
            var room = host.Session.MyPlayer.Room;
            int boss = host.Client.Start.Fighters.Single(f => f.IsBoss).Id;
            Check(Peers.Select(p => p.Client.Start.Encounter).Distinct().Count() == 1 &&
                host.Client.Start.Host == host.Session.MyPlayer.Id, "server roster and leader binding reach all clients");

            var inputs = Peers.Select(p => p.Client.Input(InputKind.Attack, 1)).ToArray();
            Until(() => Peers.All(p => p.Events.Count(e => e.Kind == "HitResolved") == 4), "host proposals and server hits");
            Check(Peers.All(p => p.Client.Latest.Fighters.Single(f => f.Id == boss).Hp == 1100),
                "four host-computed actions finalize exactly 100 damage on every client");
            Check(Peers.Select(p => string.Join("|", p.Events.Select(e => e.ToString()))).Distinct().Count() == 1,
                "four clients have identical ordered final events");
            Check(room.CreateSnapshot().BossHp == 1100, "legacy monitoring HP mirrors only finalized core state");

            foreach(var peer in Peers) peer.InputDelayMs=20;
            var delayed=Peers.Select(p=>p.Client.Input(InputKind.Move,0)).ToArray();
            Until(()=>delayed.All(i=>host.Messages.OfType<S_RaidInput>().Any(r=>r.Input.Key.Equals(i.Key))),"delayed timestamped inputs");
            Check(delayed.All(i=>host.Messages.OfType<S_RaidInput>().Any(r=>r.Input.Key.Equals(i.Key) && r.Result==(int)Admission.Admitted &&
                !r.UsedReceiveTime && r.EffectiveTick<r.ReceivedUs/host.Client.Start.TickUs+1)),"all four real TCP inputs preserve pre-delay tick with server-issued clock mapping");
            foreach(var peer in Peers) peer.InputDelayMs=0;
            Peers[0].Send(inputs[0]); RunFor(120);
            Check(Peers[0].Messages.OfType<S_RaidInput>().Any(r => r.Result == (int)Admission.Duplicate), "duplicate input returns cached receipt");
            Check(Peers.All(p => p.Events.Count(e => e.Kind == "HitResolved") == 4), "duplicate input never repeats damage");
            var conflict = inputs[0].Clone(); conflict.Value = 4; Peers[0].Send(conflict);
            Until(() => host.Messages.OfType<S_RaidInput>().Any(r => r.Result == (int)Admission.Conflict), "conflicting input");
            var spoof = inputs[0].Clone(); spoof.Key.Player = other.Session.MyPlayer.Id; spoof.Key.Sequence = 999;
            host.Send(spoof);
            Until(() => host.Messages.OfType<S_RaidInput>().Any(r => r.Result == (int)Admission.WrongSession), "spoof rejection");
            var stale = other.Client.Input(InputKind.Move, 0); stale.Key.Sequence += 900; stale.Key.Epoch++;
            other.Send(stale);
            Until(() => host.Messages.OfType<S_RaidInput>().Any(r => r.Input.Key.Epoch == 2 && r.Result == (int)Admission.WrongSession), "stale epoch");
            Check(true, "changed payload, foreign player and stale connection epoch rejected over TCP");

            var proposal = new C_RaidProposal { Encounter = host.Client.Start.Encounter, HostEpoch = 1, Sequence = 500,
                StartTick = host.Client.Latest.FinalizedTick + 20, Actor = other.Session.MyPlayer.Id, Definition = 1,
                Origin = inputs[1].Key.Clone() };
            other.Send(proposal);
            Until(() => host.Messages.OfType<S_RaidProposalResult>().Any(p => p.Sequence == 500 && p.Result == (int)Admission.WrongHost), "nonhost rejection");
            proposal.Sequence = 501; proposal.Actor = boss; proposal.Origin = null; proposal.Definition = 99;
            host.Send(proposal);
            Until(() => host.Messages.OfType<S_RaidProposalResult>().Any(p => p.Sequence == 501 && p.Result == (int)Admission.Invalid), "undefined pattern rejection");
            Check(true, "nonhost proposals and undefined boss damage rejected");

            host.Send(new C_RaidProposal { Encounter = host.Client.Start.Encounter, HostEpoch = 1, Sequence = 503,
                StartTick = host.Client.Latest.ServerTimeUs / host.Client.Start.TickUs + 10,
                Actor = other.Session.MyPlayer.Id, Definition = 1,
                Origin = new RaidInputKey { Encounter = host.Client.Start.Encounter, Player = other.Session.MyPlayer.Id, Epoch = 1, Sequence = 99999 } });
            Until(() => other.Events.Any(e => e.Kind == "ActionRejected" && e.Key.EndsWith("/a503")), "fabricated action");
            Check(other.Client.Latest.Fighters.Single(f => f.Id == boss).Hp == 1100,
                "even the host cannot create player damage without a matching admitted input");

            other.Send(new C_Move { PosInfo = new PositionInfo { PosX = 10000 } });
            other.Send(new C_Collision { Playerinfo = new ObjectInfo { ObjectId = boss, Damage = 99999 } });
            VerifyUdpGuard(other);
            // Same HandleMove endpoint reached by UDP; directly exercise the room guard as well as TCP dispatch.
            room.Push(room.HandleMove, other.Session.MyPlayer, new C_Move { PosInfo = new PositionInfo { PosX = 10000 } });
            RunFor(100);
            Check(room.CreateSnapshot().BossHp == 1100 && other.Session.MyPlayer.PosInfo.PosX == 0,
                "legacy collision and TCP/UDP movement endpoints cannot bypass the core");

            other.Client.Input(InputKind.Jump);
            Until(() => other.Client.Latest.Fighters.Single(f => f.Id == other.Session.MyPlayer.Id).JumpHeight > 0, "jump state");
            Check(true, "jump input is finalized and replicated through the same core");
            RunFor(200);
            other.Client.Input(InputKind.Move, 1);
            Until(() => other.Client.Latest.Fighters.Single(f => f.Id == other.Session.MyPlayer.Id).X > 0, "move state");
            RunFor(150);
            int stoppedX = other.Client.Latest.Fighters.Single(f => f.Id == other.Session.MyPlayer.Id).X;
            RunFor(100);
            Check(stoppedX > 0 && other.Client.Latest.Fighters.Single(f => f.Id == other.Session.MyPlayer.Id).X == stoppedX,
                "network movement is server-integrated and stops after the input lease");
            foreach (var peer in Peers) peer.Client.Input(InputKind.Attack, 4);
            Until(() => Peers.All(p => p.Events.Count(e => e.Kind == "HitResolved") == 16), "four multi-hit attacks");
            Check(Peers.All(p => p.Client.Latest.Fighters.Single(f => f.Id == boss).Hp == 980),
                "four three-hit actions resolve 12 hits without replay duplicates");
            var replayFrame = other.Client.Latest.Clone(); int before = other.Events.Count;
            other.Client.Handle(replayFrame); other.Client.Handle(replayFrame);
            Check(other.Events.Count == before, "client final event sequence suppresses duplicate presentation callbacks");

            host.Client.AutoPropose = false;
            var omitted = other.Client.Input(InputKind.Attack, 1);
            Until(() => other.Events.Any(e => e.Kind == "InputRejected" && e.Origin?.Sequence == omitted.Key.Sequence &&
                e.Origin?.Player == omitted.Key.Player), "missing host proposal");
            Check(other.Client.Latest.Fighters.Single(f => f.Id == boss).Hp == 980,
                "server does not invent attacks when the host omits its proposal");
            host.Client.AutoPropose = true;
            long oldTick = other.Messages.OfType<S_RaidInput>().Last(r => r.Input.Key.Equals(omitted.Key)).EffectiveTick;
            host.Send(new C_RaidProposal { Encounter = host.Client.Start.Encounter, HostEpoch = 1, Sequence = 502,
                StartTick = oldTick, Actor = omitted.Key.Player, Definition = 1, Origin = omitted.Key.Clone() });
            Until(() => host.Messages.OfType<S_RaidProposalResult>().Any(p => p.Sequence == 502 && p.Result == (int)Admission.TooLate), "late host proposal");
            Check(other.Client.Latest.Fighters.Single(f => f.Id == boss).Hp == 980,
                "late proposal cannot rewrite already-finalized damage");

            var clockLie = omitted.Clone(); clockLie.Kind = (int)InputKind.Move; clockLie.Value = 0;
            clockLie.Key.Sequence = 10000; clockLie.ClientTimeUs = long.MaxValue;
            other.Send(clockLie);
            Until(() => other.Messages.OfType<S_RaidInput>().Any(r => r.Input.Key.Sequence == 10000), "untrusted timestamp");
            var clockReceipt = other.Messages.OfType<S_RaidInput>().Last(r => r.Input.Key.Sequence == 10000);
            Check(clockReceipt.Result==(int)Admission.Invalid || clockReceipt.Result==(int)Admission.FutureTimestamp,
                "measured connection rejects forged future timestamp");
            clockLie.Key.Sequence++;clockLie.ClockVersion=1;other.Send(clockLie);
            Until(()=>other.Messages.OfType<S_RaidInput>().Any(r=>r.Input.Key.Sequence==10001),"fallback timestamp");
            clockReceipt=other.Messages.OfType<S_RaidInput>().Last(r=>r.Input.Key.Sequence==10001);
            Check(clockReceipt.Result == (int)Admission.Admitted && clockReceipt.UsedReceiveTime &&
                clockReceipt.EffectiveTick == clockReceipt.ReceivedUs / other.Client.Start.TickUs + 1,
                "untrusted client timestamp cannot choose the server evaluation tick");

            var missing = Peers.First(p => p != host && p != other);
            missing.Close(); RunFor(100);
            Check(room.PlayerCount == 4 && other.Client.Start != null && room.CreateSnapshot().BossHp == 980,
                "nonhost socket loss leaves the character and boss HP unchanged");
            host.Close();
            Until(() => other.Messages.OfType<S_RaidEnded>().Any() && other.Messages.OfType<S_SceneMove>().Count() >= 2,
                "host loss and return");
            var back = other.Messages.OfType<S_SceneMove>().Last(); other.Ready(back);
            Until(() => other.Session.MyPlayer.Room?.RoomType == RoomType.Town, "return to town ready");
            Check(other.Client.Start == null && other.Session.MyPlayer.Room.RoomType == RoomType.Town,
                "host disconnect aborts combat and remaining client completes real town transfer");
            foreach (var p in Peers) p.Close();
            RunFor(50); Peers.Clear();
            VerifyMatchIsolationAndLoadingLoss();
            foreach (var p in Peers) p.Close();
            RunFor(50); Peers.Clear();
            VerifyHostOmissionLimit();
            Console.WriteLine($"[DAWN_NETWORK_VALIDATED] {_passed} checks; real four-player TCP parties + UDP; production matchmaking/transfer/packet/core paths; DB login fixture only");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine("FAIL: " + e); return 1; }
        finally { foreach (var p in Peers) p.Close(); }
    }
    private static void Check(bool value, string name)
    { if (!value) throw new Exception(name); _passed++; Console.WriteLine("PASS: " + name); }
    private static void Step()
    {
        RoomManager.Instance.UpdateRooms();
        foreach (var peer in Peers) peer.Pump();
        Thread.Sleep(5);
    }
    private static void RunFor(int ms) { var watch = Stopwatch.StartNew(); while (watch.ElapsedMilliseconds < ms) Step(); }
    private static void Until(Func<bool> done, string name)
    { var watch = Stopwatch.StartNew(); while (!done()) { if (watch.ElapsedMilliseconds > 6000) throw new Exception("Timeout: " + name); Step(); } }

    private static void VerifyUdpGuard(Peer peer)
    {
        using (var sender = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)))
        using (var receiver = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)))
        {
            sender.Client.ReceiveTimeout = receiver.Client.ReceiveTimeout = 1000;
            var handler = new UdpGamePacketHandler();
            handler.SetSender((data, remote) => receiver.Send(data, data.Length, (IPEndPoint)remote));
            peer.Session.UdpToken = Guid.NewGuid().ToString("N");
            peer.Session.UdpTokenExpiresAt = DateTime.UtcNow.AddMinutes(1);
            Action<MsgId, IMessage> dispatch = (id, message) => {
                byte[] body = message.ToByteArray(), bytes = new byte[body.Length + 2];
                Array.Copy(BitConverter.GetBytes((ushort)id), bytes, 2); Array.Copy(body, 0, bytes, 2, body.Length);
                sender.Send(bytes, bytes.Length, (IPEndPoint)receiver.Client.LocalEndPoint);
                IPEndPoint endpoint = new IPEndPoint(IPAddress.Any, 0);
                byte[] incoming = receiver.Receive(ref endpoint);
                if (!handler.HandleDatagram(incoming, incoming.Length, endpoint)) throw new Exception("UDP dispatch failed");
            };
            dispatch(MsgId.CUdpHello, new C_UdpHello { Token = peer.Session.UdpToken });
            IPEndPoint replyEndpoint = new IPEndPoint(IPAddress.Any, 0);
            byte[] reply = sender.Receive(ref replyEndpoint);
            Check(S_UdpHello.Parser.ParseFrom(reply, 2, reply.Length - 2).Ok, "real UDP socket endpoint authenticates with the session token");
            dispatch(MsgId.CUdpMove, new C_UdpMove { Sequence = 1, PosInfo = new PositionInfo { PosX = 1 } });
            RunFor(50);
            Check(peer.Session.MyPlayer.PosInfo.PosX == 0 && !peer.Session.HasLastAcceptedUdpMove,
                "authenticated UDP packet cannot create a second movement authority");
        }
    }

    private static void VerifyMatchIsolationAndLoadingLoss()
    {
        var town = RoomManager.Instance.Find(RoomType.Town);
        for (int i = 0; i < 5; i++)
        {
            var peer = new Peer(100 + i); Peers.Add(peer); town.EnterRoom(peer.Session.MyPlayer);
            if (i < 4) peer.Client.Negotiate();
        }
        var raidPeers = Peers.Take(4).ToArray();
        Until(() => raidPeers.All(p => p.Messages.OfType<S_RaidHello>().Any()), "second party negotiation");
        var rejected = raidPeers[0]; rejected.Session.AccountDbId = 0; rejected.Send(new C_CreateRoom());
        Until(() => rejected.Messages.OfType<S_CreateRoom>().Any(p => p.ResponseCode == 0), "unauthenticated match");
        Check(!rejected.Session.RaidMatchRequested, "new raid matchmaking requires server-authenticated town identity");
        rejected.Session.AccountDbId = 100;
        rejected.Session.MyPlayer.Inven.Items[100].Equipped = false; rejected.Send(new C_CreateRoom());
        Until(() => rejected.Messages.OfType<S_CreateRoom>().Count(p => p.ResponseCode == 0) == 2, "equipment rejection");
        Check(!rejected.Session.RaidMatchRequested, "existing equipped weapon/armor matching requirement remains enforced");
        rejected.Session.MyPlayer.Inven.Items[100].Equipped = true;
        foreach (var peer in raidPeers.Take(3)) peer.Send(new C_CreateRoom());
        Peers[4].Send(new C_CreateRoom()); RunFor(100);
        Check(Peers.All(p => !p.Messages.OfType<S_SceneMove>().Any()), "legacy client cannot fill a new-core party slot");
        raidPeers[0].Send(new C_RaidHello { Version = 0 });
        Until(() => raidPeers[0].Messages.OfType<S_RaidHello>().Any(p => p.Reason == "NegotiationLocked"), "locked protocol");
        Check(raidPeers[0].Session.RaidProtocolVersion == 1, "queued clients cannot downgrade their negotiated protocol");
        raidPeers[3].Send(new C_CreateRoom());
        Until(() => raidPeers.All(p => p.Messages.OfType<S_SceneMove>().Any()), "second four-player match");
        var moves = raidPeers.Select(p => p.Messages.OfType<S_SceneMove>().Single()).ToArray();
        var target = RoomManager.Instance.Find(moves[0].TargetRoomId);
        target.EnterRoom(Peers[4].Session.MyPlayer);
        Check(target.PlayerCount == 0 && Peers[4].Session.MyPlayer.Room == town,
            "matched room refuses an outside player even at the room entry boundary");
        var host = raidPeers.Single(p => p.Session.MyPlayer.Info.IsMaster);
        var survivor = raidPeers.First(p => p != host);
        var staleMove = survivor.Messages.OfType<S_SceneMove>().Single();
        host.Close();
        Until(() => survivor.Messages.OfType<S_SceneMove>().Count() >= 2, "loading host disconnect");
        Check(raidPeers.All(p => p.Client.Start == null), "loading-stage host loss never starts a partial encounter");
        var back = survivor.Messages.OfType<S_SceneMove>().Last();
        survivor.Ready(staleMove); RunFor(50);
        Check(survivor.Session.IsTransferring && survivor.Session.PendingRoomId == back.TargetRoomId,
            "old scene-ready cannot resurrect an aborted raid");
        survivor.Ready(back);
        Until(() => survivor.Session.MyPlayer.Room?.RoomType == RoomType.Town, "loading abort town return");
        Check(true, "loading abort completes town return without entering the dead raid");
    }

    private static void VerifyHostOmissionLimit()
    {
        var town = RoomManager.Instance.Find(RoomType.Town);
        for (int i = 0; i < 4; i++)
        {
            var peer = new Peer(200 + i); Peers.Add(peer); town.EnterRoom(peer.Session.MyPlayer); peer.Client.Negotiate();
        }
        Until(() => Peers.All(p => p.Messages.OfType<S_RaidHello>().Any()), "omission-party negotiation");
        foreach (var peer in Peers) peer.Send(new C_CreateRoom());
        Until(() => Peers.All(p => p.Messages.OfType<S_SceneMove>().Any()), "omission-party match");
        foreach (var peer in Peers) peer.Ready(peer.Messages.OfType<S_SceneMove>().Single());
        Until(() => Peers.All(p => p.Client.Start != null), "omission-party start");
        var host = Peers.Single(p => p.Client.IsHost); host.Client.AutoPropose = false;
        foreach (var peer in Peers.Where(p => p != host)) peer.Client.Input(InputKind.Attack, 1);
        Until(() => Peers.All(p => p.Messages.OfType<S_RaidEnded>().Any()), "host omission limit");
        Check(Peers.All(p => p.Messages.OfType<S_RaidEnded>().Last().Reason == "HostProposalOmissions") &&
            Peers.All(p => p.Events.All(e => e.Kind != "HitResolved")),
            "three missing host proposals end the encounter without fabricated damage or rewards");
    }

    private sealed class Peer
    {
        public readonly ClientSession Session;
        public readonly RaidNetworkClient Client = new RaidNetworkClient();
        public readonly List<IMessage> Messages = new List<IMessage>();
        public readonly List<RaidEvent> Events = new List<RaidEvent>();
        private readonly Socket _socket;
        private readonly List<byte> _buffer = new List<byte>();
        private bool _closed;
        public int InputDelayMs;
        private readonly Stopwatch delayClock=Stopwatch.StartNew();
        private readonly Queue<(long Due,IMessage Message)> delayedInputs=new Queue<(long,IMessage)>();
        public Peer(int identity)
        {
            Session = SessionManager.Instance.Generate();
            Session.AccountDbId = identity;
            var player = ObjectManager.Instance.Add<Player>();
            player.PlayerDbId = identity; player.Info.Name = "NetworkFixture" + identity; player.Session = Session;
            player.Inven.Add(new Weapon(1) { ItemDbId = 1, Equipped = true });
            player.Inven.Add(new Armor(100) { ItemDbId = 100, Equipped = true });
            Session.MyPlayer = player;
            var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true, SendTimeout = 1000 };
            _socket.Connect(listener.LocalEndpoint);
            var accepted = listener.AcceptSocket(); accepted.NoDelay = true; listener.Stop();
            Session.Start(accepted);
            Client.Send = Send; Client.Committed += e => Events.Add(e);
        }
        public void Ready(S_SceneMove move) => Send(new C_SceneReady { TargetRoomId = move.TargetRoomId,
            TransferId = move.TransferId, TargetRoomType = move.TargetRoomType, SceneType = move.SceneType });
        public void Send(IMessage message)
        {
            if(InputDelayMs>0 && message is C_RaidInput) {delayedInputs.Enqueue((delayClock.ElapsedMilliseconds+InputDelayMs,message));return;}
            Write(message);
        }
        private void Write(IMessage message)
        {
            var body = message.ToByteArray(); var bytes = new byte[body.Length + 4];
            ushort id = (ushort)(MsgId)Enum.Parse(typeof(MsgId), message.Descriptor.Name.Replace("_", ""));
            Array.Copy(BitConverter.GetBytes((ushort)bytes.Length), bytes, 2);
            Array.Copy(BitConverter.GetBytes(id), 0, bytes, 2, 2); Array.Copy(body, 0, bytes, 4, body.Length);
            int sent = 0; while (sent < bytes.Length) sent += _socket.Send(bytes, sent, bytes.Length - sent, SocketFlags.None);
        }
        public void Pump()
        {
            if (_closed) return;
            while(delayedInputs.Count>0 && delayedInputs.Peek().Due<=delayClock.ElapsedMilliseconds) Write(delayedInputs.Dequeue().Message);
            while (_socket.Available > 0) { var data = new byte[Math.Min(65536, _socket.Available)]; int n = _socket.Receive(data); _buffer.AddRange(data.Take(n)); }
            while (_buffer.Count >= 4)
            {
                int size = _buffer[0] | (_buffer[1] << 8); if (_buffer.Count < size) return;
                if (size < 4) throw new Exception("Bad frame");
                int id = _buffer[2] | (_buffer[3] << 8); var data = _buffer.Skip(4).Take(size - 4).ToArray(); _buffer.RemoveRange(0, size);
                IMessage message = Parse((MsgId)id, data); if (message == null) continue;
                Messages.Add(message); Client.Handle(message);
            }
        }
        private static IMessage Parse(MsgId id, byte[] data)
        {
            switch (id)
            {
                case MsgId.SRaidClockChallenge:return S_RaidClockChallenge.Parser.ParseFrom(data);
                case MsgId.SRaidClockState:return S_RaidClockState.Parser.ParseFrom(data);
                case MsgId.SRaidHello: return S_RaidHello.Parser.ParseFrom(data);
                case MsgId.SRaidStart: return S_RaidStart.Parser.ParseFrom(data);
                case MsgId.SRaidInput: return S_RaidInput.Parser.ParseFrom(data);
                case MsgId.SRaidProposalResult: return S_RaidProposalResult.Parser.ParseFrom(data);
                case MsgId.SRaidFrame: return S_RaidFrame.Parser.ParseFrom(data);
                case MsgId.SRaidEnded: return S_RaidEnded.Parser.ParseFrom(data);
                case MsgId.SSceneMove: return S_SceneMove.Parser.ParseFrom(data);
                case MsgId.SEnterParty: return S_EnterParty.Parser.ParseFrom(data);
                case MsgId.SCreateRoom: return S_CreateRoom.Parser.ParseFrom(data);
                default: return null;
            }
        }
        public void Close() { if (_closed) return; _closed = true; _socket.Dispose(); }
    }
}
