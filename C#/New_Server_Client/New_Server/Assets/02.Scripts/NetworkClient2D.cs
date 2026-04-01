using Google.Protobuf;
using Server.Protocol;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

public class NetworkClient2D : MonoBehaviour
{
    [Header("Connection")]
    [SerializeField] private string _host = "127.0.0.1";
    [SerializeField] private int _port = 8080;

    [Header("Refs")]
    [SerializeField] private WorldState2D _worldState;
    [SerializeField] private CombatEventPresenter2D _combatEventPresenter;

    private Socket _socket;
    private Thread _recvThread;
    private volatile bool _running;

    private readonly Queue<Action> _mainThreadActions = new Queue<Action>();
    private readonly object _queueLock = new object();

    private uint _inputSeq = 0;
    private int _clientTick = 0;
    private uint _pingSeq = 1;

    public bool IsConnected => _socket != null && _socket.Connected;

    private void Start()
    {
        Connect();
    }

    private void Update()
    {
        FlushMainThreadActions();
    }

    private void OnDestroy()
    {
        Shutdown();
    }

    public void Connect()
    {
        if (IsConnected)
            return;

        try
        {
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _socket.NoDelay = true;
            _socket.Connect(new IPEndPoint(IPAddress.Parse(_host), _port));

            _running = true;
            _recvThread = new Thread(ReceiveLoop);
            _recvThread.IsBackground = true;
            _recvThread.Start();

            Debug.Log($"[Network] Connected to {_host}:{_port}");

            SendPing();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Network] Connect failed: {ex}");
        }
    }

    public void Shutdown()
    {
        _running = false;

        try
        {
            if (_socket != null)
            {
                if (_socket.Connected)
                    _socket.Shutdown(SocketShutdown.Both);

                _socket.Close();
                _socket = null;
            }
        }
        catch
        {
        }

        try
        {
            if (_recvThread != null && _recvThread.IsAlive)
                _recvThread.Join(200);
        }
        catch
        {
        }
    }

    private void SendPing()
    {
        C_Ping ping = new C_Ping
        {
            Sequence = _pingSeq++,
            Message = "hello from unity"
        };

        SendPacket(MsgId.CPing, ping);
    }

    public void SendMoveInput(int moveX, int moveY)
    {
        if (!IsConnected)
            return;

        C_MoveInput packet = new C_MoveInput
        {
            InputSeq = ++_inputSeq,
            ClientTick = _clientTick++,
            MoveX = moveX,
            MoveY = moveY
        };

        SendPacket(MsgId.CMoveInput, packet);
    }

    public void SendAction(ActionType actionType, int dirX, int dirY)
    {
        if (!IsConnected)
            return;

        C_ActionInput packet = new C_ActionInput
        {
            InputSeq = ++_inputSeq,
            ActionType = actionType,
            DirX = dirX,
            DirY = dirY
        };

        SendPacket(MsgId.CActionInput, packet);
    }

    private void SendPacket(MsgId msgId, IMessage packet)
    {
        try
        {
            byte[] payload = packet.ToByteArray();

            ushort size = (ushort)(payload.Length + 4);
            ushort packetId = (ushort)msgId;

            byte[] buffer = new byte[size];

            Array.Copy(BitConverter.GetBytes(size), 0, buffer, 0, 2);
            Array.Copy(BitConverter.GetBytes(packetId), 0, buffer, 2, 2);
            Array.Copy(payload, 0, buffer, 4, payload.Length);

            _socket.Send(buffer);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Network] Send failed: {ex}");
        }
    }

    private void ReceiveLoop()
    {
        try
        {
            while (_running)
            {
                MsgId packetId = ReceiveOnePacket(out byte[] payload);

                switch (packetId)
                {
                    case MsgId.SPong:
                        {
                            S_Pong pong = S_Pong.Parser.ParseFrom(payload);
                            EnqueueMainThread(() =>
                            {
                                Debug.Log($"[Network] Pong received seq={pong.Sequence} msg={pong.Message}");
                            });
                            break;
                        }

                    case MsgId.SRoomSnapshot:
                        {
                            S_RoomSnapshot snapshot = S_RoomSnapshot.Parser.ParseFrom(payload);

                            int serverTick = snapshot.ServerTick;
                            uint ack = snapshot.AckInputSeq;

                            List<ActorStateData> incomingActors = new List<ActorStateData>(snapshot.Actors.Count);
                            foreach (ActorSnapshot actor in snapshot.Actors)
                            {
                                incomingActors.Add(new ActorStateData
                                {
                                    ActorId = actor.ActorId,
                                    ActorType = actor.ActorType,
                                    MainState = actor.MainState,
                                    PosX = actor.Pos.X,
                                    PosY = actor.Pos.Y,
                                    Hp = actor.Hp,
                                    MaxHp = actor.MaxHp,
                                    IsDead = actor.IsDead,
                                    IsJumping = actor.IsJumping
                                });
                            }

                            EnqueueMainThread(() =>
                            {
                                _worldState.ApplySnapshot(serverTick, ack, incomingActors);
                            });
                            break;
                        }

                    case MsgId.SCombatEvents:
                        {
                            S_CombatEvents packet = S_CombatEvents.Parser.ParseFrom(payload);

                            int serverTick = packet.ServerTick;
                            List<CombatEventData> events = new List<CombatEventData>(packet.Events.Count);

                            foreach (CombatEvent ev in packet.Events)
                            {
                                events.Add(new CombatEventData
                                {
                                    EventType = ev.EventType,
                                    AttackerId = ev.AttackerId,
                                    TargetId = ev.TargetId,
                                    ActionType = ev.ActionType,
                                    Value = ev.Value
                                });
                            }

                            EnqueueMainThread(() =>
                            {
                                _combatEventPresenter.Present(serverTick, events);
                            });
                            break;
                        }

                    case MsgId.SPatternZones:
                        {
                            S_PatternZones packet = S_PatternZones.Parser.ParseFrom(payload);

                            int serverTick = packet.ServerTick;
                            int ownerEnemyId = packet.OwnerEnemyId;
                            ActionType actionType = packet.ActionType;
                            int durationTick = packet.DurationTick;

                            List<PatternZoneData> zones = new List<PatternZoneData>(packet.Zones.Count);
                            foreach (ZoneInfo zone in packet.Zones)
                            {
                                zones.Add(new PatternZoneData
                                {
                                    ZoneId = zone.ZoneId,
                                    PosX = zone.Pos.X,
                                    PosY = zone.Pos.Y,
                                    Radius = zone.Radius
                                });
                            }

                            EnqueueMainThread(() =>
                            {
                                Debug.Log($"[PatternZonesRecv] tick={serverTick} owner={ownerEnemyId} action={actionType} zoneCount={zones.Count}");

                                if (_combatEventPresenter != null)
                                    _combatEventPresenter.PresentPatternZones(serverTick, ownerEnemyId, actionType, zones, durationTick);
                            });
                            break;
                        }

                    default:
                        {
                            MsgId unknown = packetId;
                            EnqueueMainThread(() =>
                            {
                                Debug.Log($"[Network] Unknown packet: {unknown}");
                            });
                            break;
                        }
                }
            }
        }
        catch (Exception ex)
        {
            if (_running)
            {
                EnqueueMainThread(() =>
                {
                    Debug.LogError($"[Network] Receive loop stopped: {ex}");
                });
            }
        }
    }

    private MsgId ReceiveOnePacket(out byte[] payload)
    {
        byte[] headerBuffer = ReceiveExactly(4);

        ushort size = BitConverter.ToUInt16(headerBuffer, 0);
        ushort packetId = BitConverter.ToUInt16(headerBuffer, 2);

        int payloadSize = size - 4;
        payload = ReceiveExactly(payloadSize);

        return (MsgId)packetId;
    }

    private byte[] ReceiveExactly(int size)
    {
        byte[] buffer = new byte[size];
        int received = 0;

        while (received < size)
        {
            int recv = _socket.Receive(buffer, received, size - received, SocketFlags.None);
            if (recv == 0)
                throw new Exception("Disconnected from server.");

            received += recv;
        }

        return buffer;
    }

    private void EnqueueMainThread(Action action)
    {
        lock (_queueLock)
        {
            _mainThreadActions.Enqueue(action);
        }
    }

    private void FlushMainThreadActions()
    {
        while (true)
        {
            Action action = null;

            lock (_queueLock)
            {
                if (_mainThreadActions.Count == 0)
                    break;

                action = _mainThreadActions.Dequeue();
            }

            action?.Invoke();
        }
    }
}