using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ServerCore
{
    public class UdpListener
    {

        Socket _socket;
        CancellationTokenSource _cts;
        Task _recvTask;
        public Func<byte[], int, EndPoint, bool> UdpDatagramHandler { get; set; }
        const int UnknownDatagramLogWindowMs = 10000;
        const int MaxUnknownDatagramLogsPerWindow = 5;
        readonly object _unknownLogLock = new object();
        readonly Dictionary<string, UnknownLogState> _unknownLogStates = new Dictionary<string, UnknownLogState>();

        class UnknownLogState
        {
            public DateTime WindowStartedAtUtc;
            public int Count;
            public int SuppressedCount;
        }

        public void Init(IPEndPoint endPoint)
        {
            Close();

            _socket = new Socket(endPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            _socket.Bind(endPoint);

            _cts = new CancellationTokenSource();
            _recvTask = Task.Run(() => RecvLoop(_cts.Token));
        }

        async Task RecvLoop(CancellationToken token)
        {
            byte[] recvBuffer = new byte[65535];
            EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

            while (token.IsCancellationRequested == false)
            {
                try
                {
                    Socket socket = _socket;
                    if (socket == null)
                        break;

                    SocketReceiveFromResult result = await socket.ReceiveFromAsync(
                        new ArraySegment<byte>(recvBuffer),
                        SocketFlags.None,
                        remoteEndPoint);

                    if (UdpDatagramHandler?.Invoke(recvBuffer, result.ReceivedBytes, result.RemoteEndPoint) == true)
                        continue;

                    if (ShouldLogUnknownDatagram(result.RemoteEndPoint, out int suppressed))
                        Console.WriteLine($"[UDP] Unknown datagram ignored. EndPoint={result.RemoteEndPoint}, Size={result.ReceivedBytes}, Suppressed={suppressed}");
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (SocketException e)
                {
                    if (token.IsCancellationRequested)
                        break;

                    Console.WriteLine($"UDP Receive Failed : {e.SocketErrorCode}");
                }
                catch (Exception e)
                {
                    if (token.IsCancellationRequested)
                        break;

                    Console.WriteLine($"UDP Receive Failed : {e}");
                }
            }
        }

        bool ShouldLogUnknownDatagram(EndPoint remoteEndPoint, out int suppressed)
        {
            suppressed = 0;
            string key = remoteEndPoint?.ToString() ?? string.Empty;
            DateTime nowUtc = DateTime.UtcNow;

            lock (_unknownLogLock)
            {
                if (_unknownLogStates.TryGetValue(key, out UnknownLogState state) == false ||
                    (nowUtc - state.WindowStartedAtUtc).TotalMilliseconds >= UnknownDatagramLogWindowMs)
                {
                    state = new UnknownLogState
                    {
                        WindowStartedAtUtc = nowUtc,
                        Count = 0,
                        SuppressedCount = 0
                    };
                    _unknownLogStates[key] = state;
                }

                state.Count++;
                if (state.Count <= MaxUnknownDatagramLogsPerWindow)
                {
                    suppressed = state.SuppressedCount;
                    state.SuppressedCount = 0;
                    return true;
                }

                state.SuppressedCount++;
                return false;
            }
        }

        public void SendTo(byte[] buffer, EndPoint remoteEndPoint)
        {
            Socket socket = _socket;
            if (socket == null)
                return;

            try
            {
                socket.SendTo(buffer, remoteEndPoint);
            }
            catch (Exception e)
            {
                Console.WriteLine($"[UDP] SendTo failed. EndPoint={remoteEndPoint}, Error={e.Message}");
            }
        }

        public void Close()
        {
            Socket socket = _socket;
            CancellationTokenSource cts = _cts;
            Task recvTask = _recvTask;

            if (socket == null && cts == null && recvTask == null)
                return;

            cts?.Cancel();

            try
            {
                socket?.Close();
            }
            catch (ObjectDisposedException)
            {
                // Normal during shutdown.
            }
            catch (SocketException)
            {
                // Normal during shutdown.
            }
            catch
            {
                // Ignore shutdown errors.
            }

            try
            {
                recvTask?.Wait(TimeSpan.FromSeconds(1));
            }
            catch (AggregateException e)
            {
                e.Handle(ex => ex is ObjectDisposedException || ex is SocketException);
            }
            catch (ObjectDisposedException)
            {
                // Normal during shutdown.
            }
            finally
            {
                _socket = null;
                _recvTask = null;

                cts?.Dispose();
                _cts = null;
            }
        }
    }
}
