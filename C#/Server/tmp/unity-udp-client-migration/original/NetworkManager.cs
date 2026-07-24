//using System;
//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;
//using ServerCore;
//using System.Net;
//using Google.Protobuf;
//using Google.Protobuf.Protocol;

//public class NetworkManager
//{

//    public ApiHelper apiHelper = new ApiHelper();

//    ServerSession _session = new ServerSession();
//    public void Send(IMessage packet)
//    {
//        _session.Send(packet);
//    }
//    public void ConnectToGame()
//    {
//        Connector connector = new Connector();

//        string host = Dns.GetHostName();
//        IPHostEntry ipHost = Dns.GetHostEntry(host);
//        IPAddress ipAddr = ipHost.AddressList[0];
//        IPEndPoint endPoint = new IPEndPoint(ipAddr, 8080); //���� �׽�Ʈ (offline)        




//        connector.Connect(endPoint, () =>
//        {
//            return _session;
//        });

//    }

//    public void OnUpdate()
//    {
//        List<PacketMessage> list = PacketQueue.Instance.PopAll();

//        foreach (PacketMessage packet in list)
//        {
//            Action<PacketSession, IMessage> handler = PacketManager.Instance.GetPacketHandler(packet.Id);

//            if (handler != null)
//            {
//                handler.Invoke(_session, packet.Message);
//            }

//        }
//    }
//}
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using UnityEngine;
using ServerCore;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.Protocol;

public class NetworkManager
{
    public ApiHelper apiHelper = new ApiHelper();

    ServerSession _session = new ServerSession();
    bool _isTcpConnecting;
    readonly object _disconnectLock = new object();
    bool _disconnectPending;
    bool _disconnectHandled;
    string _disconnectEndPoint;
    // UDP �׽�Ʈ/Ȯ��� Ŭ���̾�Ʈ
    // TCP ServerSession�� �и��ؼ� �����Ѵ�.
    UdpGameClient _udpClient = new UdpGameClient();

    string _udpToken;
    string _serverHost = "127.0.0.1";
    int _tcpPort = 8080;
    int _udpPort = 8081;

    public bool UseUdpMovement { get; private set; } = false;
    public bool DebugUdpMovementLog { get; private set; } = false;
    public bool ForceInvalidUdpTokenForTest { get; private set; } = false;

    public NetworkManager()
    {
        _session.Disconnected += HandleDisconnected;
    }

    public void SetForceInvalidUdpTokenForTest(bool enabled)
    {
        ForceInvalidUdpTokenForTest = enabled;
        Debug.Log($"[UDP] ForceInvalidUdpTokenForTest={ForceInvalidUdpTokenForTest}");
    }

    public void SetUseUdpMovementForTest(bool enabled)
    {
        UseUdpMovement = enabled;
        Debug.Log($"[UDP] UseUdpMovement={UseUdpMovement}");
    }

    public void SetDebugUdpMovementLogForTest(bool enabled)
    {
        DebugUdpMovementLog = enabled;
        Debug.Log($"[UDP] DebugUdpMovementLog={DebugUdpMovementLog}");
    }


    public void SendUdpMoveForTest(Vector3 position)
    {
        SendUdpMoveForTest(position, PlayerState.Idle, MoveDir.None);
    }

    public void SendUdpMoveForTest(Vector3 position, PlayerState state, MoveDir moveDir)
    {
        _udpClient.SendMoveForTest(position, state, moveDir);
    }
    public void SetUdpToken(string udpToken)
    {
        _udpToken = udpToken?.Trim();

        Debug.Log($"[UDP] Token saved. TokenLength={_udpToken?.Length ?? 0}");
    }

    public void RegisterUdpWithSavedToken()
    {
        if (string.IsNullOrWhiteSpace(_udpToken))
        {
            Debug.LogError("[UDP] UdpToken is empty. Cannot register UDP endpoint.");
            return;
        }

        string tokenToSend = ForceInvalidUdpTokenForTest ? $"{_udpToken}_INVALID" : _udpToken;
        if (ForceInvalidUdpTokenForTest)
            Debug.LogWarning("[UDP] ForceInvalidUdpTokenForTest enabled. Sending invalid UDP token.");

        _udpClient.SendHello(tokenToSend);
    }


    public void Send(IMessage packet)
    {
        _session.Send(packet);
    }

    public void ConnectToGame()
    {
        if (_session != null && (_session.IsConnected || _isTcpConnecting))
        {
            Debug.Log($"[TCP] TCP connection already active. ConnectToGame skipped. Connected={_session.IsConnected}, Connecting={_isTcpConnecting}");
            return;
        }

        Connector connector = new Connector();
        _session.ConnectionStateChanged = connected => _isTcpConnecting = false;
        LoadServerEndpointFromCommandLine();

        IPAddress ipAddr = ResolveServerAddress(_serverHost);
        IPEndPoint endPoint = new IPEndPoint(ipAddr, _tcpPort);
        Debug.Log($"[TCP] Connecting to {endPoint}");
        _isTcpConnecting = true;

        connector.Connect(endPoint, () =>
        {
            return _session;
        });
    }

    public void ConnectUdpToGameServer()
    {
        ConnectUdpForTest(_serverHost, _udpPort);
    }

    private void LoadServerEndpointFromCommandLine()
    {
        string hostArg = GetCommandLineValue("-serverHost=");
        if (!string.IsNullOrWhiteSpace(hostArg))
            _serverHost = hostArg.Trim();

        string tcpPortArg = GetCommandLineValue("-tcpPort=");
        if (int.TryParse(tcpPortArg, out int tcpPort))
            _tcpPort = tcpPort;

        string udpPortArg = GetCommandLineValue("-udpPort=");
        if (int.TryParse(udpPortArg, out int udpPort))
            _udpPort = udpPort;
    }

    private static string GetCommandLineValue(string prefix)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (arg.StartsWith(prefix, StringComparison.Ordinal))
                return arg.Substring(prefix.Length);
        }

        return null;
    }

    private static IPAddress ResolveServerAddress(string host)
    {
        if (IPAddress.TryParse(host, out IPAddress ipAddr))
            return ipAddr;

        IPAddress[] addresses = Dns.GetHostAddresses(host);
        foreach (IPAddress address in addresses)
        {
            if (address.AddressFamily == AddressFamily.InterNetwork)
                return address;
        }

        throw new Exception($"No IPv4 address found for server host: {host}");
    }

    // �׽�Ʈ�� UDP ����
    // ������ Unity�� ���� PC���� ���� ������ �⺻�� 127.0.0.1:8081 ���
    public void ConnectUdpForTest(string serverIp = "127.0.0.1", int serverPort = 8081)
    {
        try
        {
            IPAddress ipAddr = IPAddress.Parse(serverIp);
            IPEndPoint endPoint = new IPEndPoint(ipAddr, serverPort);

            _udpClient.Connect(endPoint);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[UDP] ConnectUdpForTest failed. ServerIp={serverIp}, Port={serverPort}, Error={ex}");
        }
    }
    public void SendUdpHelloForTest(string token = null)
    {
        _udpClient.SendHello(token);
    }

    // ���߿� S_Login���� udpToken�� ������ �� �޼��带 ȣ���ϸ� ��.
    // ����� SendUdpHelloForTest�� ���� ����������, �ǹ̸� �и��صд�.
    public void RegisterUdp(string udpToken)
    {
        _udpClient.SendHello(udpToken);
    }

    public void CloseUdpForTest()
    {
        _udpClient.Close();
    }


    void HandleDisconnected(string endPoint)
    {
        lock (_disconnectLock)
        {
            if (_disconnectHandled)
                return;

            _disconnectPending = true;
            _disconnectEndPoint = endPoint;
        }
    }

    void FlushDisconnectNotice()
    {
        string endPoint = null;
        lock (_disconnectLock)
        {
            if (_disconnectPending == false || _disconnectHandled)
                return;

            _disconnectPending = false;
            _disconnectHandled = true;
            endPoint = _disconnectEndPoint;
        }

        ClientDisconnectNotifier.ShowAndQuit(endPoint);
    }
    public void OnUpdate()
    {
        FlushDisconnectNotice();

        List<PacketMessage> list = PacketQueue.Instance.PopAll();

        foreach (PacketMessage packet in list)
        {
            Action<PacketSession, IMessage> handler = PacketManager.Instance.GetPacketHandler(packet.Id);

            if (handler != null)
            {
                handler.Invoke(_session, packet.Message);
            }
        }

        // UDP ���� �޽����� ��׶��� Task���� Queue�� �װ�,
        // Unity ���� �������� OnUpdate���� ó���Ѵ�.
        _udpClient.OnUpdate();
    }
}

public class UdpGameClient
{
    UdpClient _udpClient;
    IPEndPoint _serverEndPoint;

    CancellationTokenSource _cts;
    Task _receiveTask;

    readonly ConcurrentQueue<string> _receivedMessages = new ConcurrentQueue<string>();

    public void Connect(IPEndPoint serverEndPoint)
    {
        Close();

        _serverEndPoint = serverEndPoint;
        _udpClient = new UdpClient();

        _cts = new CancellationTokenSource();
        _receiveTask = ReceiveLoopAsync(_cts.Token);

        Debug.Log($"[UDP] Test client ready. Server={_serverEndPoint}");
    }

    public void SendHello(string token = null)
    {
        string trimmedToken = token?.Trim();
        C_UdpHello helloPacket = new C_UdpHello();
        if (!string.IsNullOrWhiteSpace(trimmedToken))
            helloPacket.Token = trimmedToken;

        SendUdpPacket(MsgId.CUdpHello, helloPacket, $"Hello TokenLength={trimmedToken?.Length ?? 0}");
    }

    bool IsReadyToSend()
    {
        if (_udpClient == null)
        {
            Debug.LogError("[UDP] UdpClient is not connected. Call ConnectUdpForTest() first.");
            return false;
        }

        if (_serverEndPoint == null)
        {
            Debug.LogError("[UDP] ServerEndPoint is null.");
            return false;
        }

        return true;
    }

    void SendUdpPacket(MsgId msgId, IMessage packet, string logMessage, bool debugOnly = false)
    {
        if (!IsReadyToSend())
            return;

        byte[] payload = packet.ToByteArray();
        byte[] datagram = new byte[sizeof(ushort) + payload.Length];
        byte[] packetIdBytes = BitConverter.GetBytes((ushort)msgId);

        Buffer.BlockCopy(packetIdBytes, 0, datagram, 0, sizeof(ushort));
        Buffer.BlockCopy(payload, 0, datagram, sizeof(ushort), payload.Length);

        try
        {
            _udpClient.Send(datagram, datagram.Length, _serverEndPoint);
if (!debugOnly || GameManager.Network.DebugUdpMovementLog == true)
                Debug.Log($"[UDP] Sent Proto {msgId}: {logMessage}, Size={datagram.Length}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[UDP] SendUdpPacket failed. MsgId={msgId}, Error={ex}");
        }
    }

    async Task ReceiveLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                UdpReceiveResult result = await _udpClient.ReceiveAsync();

                if (TryEnqueueProtoMessage(result.Buffer, result.RemoteEndPoint))
                    continue;

                _receivedMessages.Enqueue($"UNKNOWN_DATAGRAM|FROM={result.RemoteEndPoint}|SIZE={result.Buffer.Length}");
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (SocketException ex)
            {
                if (!token.IsCancellationRequested)
                    _receivedMessages.Enqueue($"SOCKET_ERROR={ex.Message}");

                break;
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                    _receivedMessages.Enqueue($"ERROR={ex.Message}");
            }
        }
    }

    bool TryEnqueueProtoMessage(byte[] buffer, IPEndPoint remoteEndPoint)
    {
        if (buffer == null || buffer.Length < sizeof(ushort))
            return false;

        try
        {
            ushort packetId = BitConverter.ToUInt16(buffer, 0);
            MsgId msgId = (MsgId)packetId;

            switch (msgId)
            {
                case MsgId.SUdpHello:
                {
                    ByteString payload = ByteString.CopyFrom(buffer, sizeof(ushort), buffer.Length - sizeof(ushort));
                    S_UdpHello udpHello = S_UdpHello.Parser.ParseFrom(payload);
                    string message = udpHello.Message ?? string.Empty;
                    _receivedMessages.Enqueue($"PROTO_S_UDP_HELLO|OK={udpHello.Ok}|MSG={message}|FROM={remoteEndPoint}");
                    return true;
                }
                default:
                    return false;
            }
        }
        catch (Exception ex)
        {
            _receivedMessages.Enqueue($"PROTO_PARSE_ERROR={ex.Message}");
            return false;
        }
    }


    public void OnUpdate()
    {
        while (_receivedMessages.TryDequeue(out string rawMessage))
        {
            if (rawMessage.StartsWith("SOCKET_ERROR="))
            {
                Debug.LogError($"[UDP] Socket error: {rawMessage.Replace("SOCKET_ERROR=", "")}");
                continue;
            }

            if (rawMessage.StartsWith("ERROR="))
            {
                Debug.LogError($"[UDP] Receive failed: {rawMessage.Replace("ERROR=", "")}");
                continue;
            }

            if (rawMessage.StartsWith("PROTO_PARSE_ERROR="))
            {
                Debug.LogWarning($"[UDP] Proto receive parse failed. Error={rawMessage.Replace("PROTO_PARSE_ERROR=", "")}");
                continue;
            }

            if (rawMessage.StartsWith("PROTO_S_UDP_HELLO"))
                                                                                             {
                HandleProtoUdpHelloEvent(rawMessage);
                continue;
            }

            if (rawMessage.StartsWith("UNKNOWN_DATAGRAM"))
            {
                Debug.Log($"[UDP] Unknown datagram ignored: {rawMessage}");
                continue;
            }

            Debug.Log($"[UDP] Unhandled queued UDP message: {rawMessage}");
        }
    }

    void HandleProtoUdpHelloEvent(string rawMessage)
    {
        bool ok = rawMessage.Contains("|OK=True|");
        string message = ExtractTaggedValue(rawMessage, "|MSG=", "|FROM=");

        Debug.Log($"[UDP] S_UdpHello received. Ok={ok}, Message={message}");
        if (ok)
        {
            Debug.Log("[UDP] UDP registration success.");
        }
        else
        {
            Debug.LogWarning("[UDP] UDP registration rejected. Check token or token expiration.");
        }
    }

    string ExtractTaggedValue(string rawMessage, string startMarker, string endMarker)
    {
        int startIndex = rawMessage.IndexOf(startMarker, StringComparison.Ordinal);
        if (startIndex < 0)
            return string.Empty;

        startIndex += startMarker.Length;
        int endIndex = rawMessage.IndexOf(endMarker, startIndex, StringComparison.Ordinal);
        if (endIndex < 0)
            return rawMessage.Substring(startIndex);

        return rawMessage.Substring(startIndex, endIndex - startIndex);
    }
    public void Close()
    {
        _cts?.Cancel();

        _udpClient?.Close();
        _udpClient?.Dispose();
        _udpClient = null;

        _cts?.Dispose();
        _cts = null;

        _receiveTask = null;
        _serverEndPoint = null;

        Debug.Log("[UDP] UDP client closed.");
    }

    public void SendMoveForTest(Vector3 position, PlayerState state, MoveDir moveDir)
    {
        C_UdpMove movePacket = new C_UdpMove();
        movePacket.PosInfo = new PositionInfo();
        movePacket.PosInfo.PosX = position.x;
        movePacket.PosInfo.PosY = position.y;
        movePacket.PosInfo.State = state;
        movePacket.PosInfo.MoveDir = moveDir;

        SendUdpPacket(MsgId.CUdpMove, movePacket, $"Move Pos=({position.x},{position.y}), State={state}, MoveDir={moveDir}", GameManager.Network.DebugUdpMovementLog);
    }
}





