using System.Net.WebSockets;
using System.Text.Json;
using System.Threading.Channels;
using Kimchily.Server.Core;

namespace Kimchily.Server.Host
{
    public sealed class SocketEndpoint
    {
        private readonly RoomHub _rooms;
        private readonly ILogger<SocketEndpoint> _logger;
        private readonly IConfiguration _configuration;
        private readonly SemaphoreSlim _slots = new SemaphoreSlim(128, 128);

        public SocketEndpoint(
            RoomHub rooms,
            ILogger<SocketEndpoint> logger,
            IConfiguration configuration)
        {
            _rooms = rooms;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task RunAsync(HttpContext context)
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                return;
            }

            // Unity WebGL is served separately. Explicit origins are an extra boundary, not authentication.
            string origin = context.Request.Headers.Origin.ToString();
            string sameOrigin = $"{context.Request.Scheme}://{context.Request.Host}";
            IEnumerable<string?> allowed = _configuration
                .GetSection("Realtime:AllowedOrigins")
                .GetChildren()
                .Select(item => item.Value);

            if (origin.Length != 0
                && (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                    || (uri.Scheme != "http" && uri.Scheme != "https")
                    || uri.PathAndQuery != "/"
                    || uri.Fragment.Length != 0
                    || uri.UserInfo.Length != 0
                    || (!string.Equals(origin, sameOrigin, StringComparison.OrdinalIgnoreCase)
                        && !allowed.Contains(origin, StringComparer.OrdinalIgnoreCase))))
            {
                context.Response.StatusCode = 403;
                return;
            }

            if (!await _slots.WaitAsync(0, context.RequestAborted))
            {
                context.Response.StatusCode = 503;
                return;
            }

            try
            {
                using (WebSocket socket = await context.WebSockets.AcceptWebSocketAsync())
                using (CancellationTokenSource lifetime =
                    CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted))
                {
                    SocketPeer peer = new SocketPeer(socket, lifetime);
                    Task writer = peer.WriteAsync(lifetime.Token);
                    peer.Send(new ServerEvent("hello")
                    {
                        SelfId = peer.Id
                    });

                    try
                    {
                        await ReceiveAsync(socket, peer, lifetime.Token);
                    }
                    catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
                    {
                        // The request or outgoing writer ended this peer's lifetime.
                    }
                    catch (WebSocketException)
                    {
                        // Normal abrupt mobile/browser disconnect.
                    }
                    finally
                    {
                        // Shutdown can stop the pump before a disconnect job runs.
                        try
                        {
                            await _rooms.DisconnectAsync(peer).WaitAsync(TimeSpan.FromSeconds(2));
                        }
                        catch (Exception error) when (error is TimeoutException
                            || error is InvalidOperationException)
                        {
                            _logger.LogDebug("Room cleanup ended during shutdown or overload.");
                        }

                        peer.Complete();

                        try
                        {
                            await writer.WaitAsync(TimeSpan.FromSeconds(2));
                        }
                        catch (Exception error) when (error is TimeoutException
                            || error is OperationCanceledException
                            || error is WebSocketException)
                        {
                            // Closing continues even if the outgoing writer has already stopped.
                        }

                        await lifetime.CancelAsync();

                        if (socket.State == WebSocketState.CloseReceived)
                        {
                            try
                            {
                                await socket.CloseOutputAsync(
                                    WebSocketCloseStatus.NormalClosure,
                                    "Closed",
                                    CancellationToken.None)
                                    .WaitAsync(TimeSpan.FromSeconds(2));
                            }
                            catch (Exception error) when (error is WebSocketException
                                || error is TimeoutException)
                            {
                                socket.Abort();
                            }
                        }
                        else
                        {
                            socket.Abort();
                        }
                    }
                }
            }
            finally
            {
                _slots.Release();
            }
        }

        private async Task ReceiveAsync(
            WebSocket socket,
            SocketPeer peer,
            CancellationToken cancellation)
        {
            byte[] buffer = new byte[Protocol.MaxMessageBytes + 1];
            Queue<long> messageTimes = new Queue<long>();

            while (!cancellation.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                int length = 0;
                WebSocketReceiveResult received;

                do
                {
                    received = await socket.ReceiveAsync(
                        new ArraySegment<byte>(buffer, length, buffer.Length - length),
                        cancellation);

                    if (received.MessageType == WebSocketMessageType.Close)
                    {
                        return;
                    }

                    if (received.MessageType != WebSocketMessageType.Text)
                    {
                        peer.Fail("TEXT_REQUIRED", "텍스트 메시지만 지원합니다.");
                        return;
                    }

                    length += received.Count;
                    if (length > Protocol.MaxMessageBytes)
                    {
                        peer.Fail("MESSAGE_TOO_LARGE", "메시지가 너무 큽니다.");
                        return;
                    }
                }
                while (!received.EndOfMessage);

                long now = Environment.TickCount64;
                while (messageTimes.TryPeek(out long timestamp) && now - timestamp >= 1000)
                {
                    messageTimes.Dequeue();
                }

                if (messageTimes.Count >= 20)
                {
                    peer.Fail("RATE_LIMIT", "요청이 너무 빠릅니다.");
                    return;
                }

                messageTimes.Enqueue(now);

                ClientCommand? command;
                try
                {
                    command = JsonSerializer.Deserialize<ClientCommand>(
                        buffer.AsSpan(0, length),
                        Protocol.Json);
                }
                catch (JsonException)
                {
                    peer.Fail("INVALID_MESSAGE", "메시지 형식을 확인해 주세요.");
                    continue;
                }

                if (command == null)
                {
                    peer.Fail("INVALID_MESSAGE", "메시지가 비어 있습니다.");
                    continue;
                }

                try
                {
                    await _rooms.HandleAsync(peer, command).WaitAsync(cancellation);
                }
                catch (InvalidOperationException)
                {
                    peer.Fail("SERVER_BUSY", "서버가 바쁩니다. 다시 연결해 주세요.");
                    return;
                }
            }
        }

        private sealed class SocketPeer : IRoomPeer
        {
            private readonly WebSocket _socket;
            private readonly CancellationTokenSource _lifetime;
            private readonly Channel<ServerEvent> _outgoing =
                Channel.CreateBounded<ServerEvent>(new BoundedChannelOptions(64)
                {
                    SingleReader = true,
                    SingleWriter = false,
                    FullMode = BoundedChannelFullMode.Wait
                });

            public string Id { get; } = Guid.NewGuid().ToString("N");

            public SocketPeer(WebSocket socket, CancellationTokenSource lifetime)
            {
                _socket = socket;
                _lifetime = lifetime;
            }

            public bool Send(ServerEvent message)
            {
                if (_outgoing.Writer.TryWrite(message))
                {
                    return true;
                }

                _socket.Abort();
                _lifetime.Cancel();
                return false;
            }

            public void Fail(string code, string message)
            {
                Send(new ServerEvent("error")
                {
                    Code = code,
                    Message = message
                });
            }

            public void Complete()
            {
                _outgoing.Writer.TryComplete();
            }

            public async Task WriteAsync(CancellationToken cancellation)
            {
                try
                {
                    await foreach (ServerEvent message in _outgoing.Reader.ReadAllAsync(cancellation))
                    {
                        await _socket.SendAsync(
                            JsonSerializer.SerializeToUtf8Bytes(message, Protocol.Json),
                            WebSocketMessageType.Text,
                            true,
                            cancellation);
                    }
                }
                catch (Exception error) when (error is WebSocketException
                    || error is OperationCanceledException)
                {
                    _socket.Abort();
                    await _lifetime.CancelAsync();
                }
            }
        }
    }
}
