using System.Net.WebSockets;
using System.Text.Json;
using System.Threading.Channels;
using Kimchily.Server.Core;

namespace Kimchily.Server.Host;

public sealed class SocketEndpoint(RoomHub rooms, ILogger<SocketEndpoint> logger, IConfiguration configuration)
{
    private readonly SemaphoreSlim _slots = new(128, 128);

    public async Task RunAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest) { context.Response.StatusCode = 400; return; }
        // Unity WebGL is served separately. Explicit origins are an extra boundary, not authentication.
        var origin = context.Request.Headers.Origin.ToString();
        var sameOrigin = $"{context.Request.Scheme}://{context.Request.Host}";
        var allowed = configuration.GetSection("Realtime:AllowedOrigins").GetChildren().Select(item => item.Value);
        if (origin.Length != 0 && (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)
            || (uri.Scheme != "http" && uri.Scheme != "https") || uri.PathAndQuery != "/" || uri.Fragment.Length != 0 || uri.UserInfo.Length != 0
            || (!string.Equals(origin, sameOrigin, StringComparison.OrdinalIgnoreCase) && !allowed.Contains(origin, StringComparer.OrdinalIgnoreCase))))
        { context.Response.StatusCode = 403; return; }
        if (!await _slots.WaitAsync(0, context.RequestAborted)) { context.Response.StatusCode = 503; return; }
        try
        {
            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
            var peer = new SocketPeer(socket, lifetime);
            var writer = peer.WriteAsync(lifetime.Token);
            peer.Send(new ServerEvent("hello") { SelfId = peer.Id });
            try { await ReceiveAsync(socket, peer, lifetime.Token); }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
            catch (WebSocketException) { /* Normal abrupt mobile/browser disconnect. */ }
            finally
            {
                // Shutdown can stop the pump before a disconnect job runs.
                try { await rooms.DisconnectAsync(peer).WaitAsync(TimeSpan.FromSeconds(2)); }
                catch (Exception error) when (error is TimeoutException or InvalidOperationException) { logger.LogDebug("Room cleanup ended during shutdown or overload."); }
                peer.Complete();
                try { await writer.WaitAsync(TimeSpan.FromSeconds(2)); }
                catch (Exception error) when (error is TimeoutException or OperationCanceledException or WebSocketException) { }
                await lifetime.CancelAsync();
                if (socket.State == WebSocketState.CloseReceived)
                {
                    try { await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Closed", CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2)); }
                    catch (Exception error) when (error is WebSocketException or TimeoutException) { socket.Abort(); }
                }
                else socket.Abort();
            }
        }
        finally { _slots.Release(); }
    }

    private async Task ReceiveAsync(WebSocket socket, SocketPeer peer, CancellationToken cancellation)
    {
        var buffer = new byte[Protocol.MaxMessageBytes + 1];
        var messageTimes = new Queue<long>();
        while (!cancellation.IsCancellationRequested && socket.State == WebSocketState.Open)
        {
            var length = 0;
            WebSocketReceiveResult received;
            do
            {
                received = await socket.ReceiveAsync(new ArraySegment<byte>(buffer, length, buffer.Length - length), cancellation);
                if (received.MessageType == WebSocketMessageType.Close) return;
                if (received.MessageType != WebSocketMessageType.Text) { peer.Fail("TEXT_REQUIRED", "텍스트 메시지만 지원합니다."); return; }
                length += received.Count;
                if (length > Protocol.MaxMessageBytes) { peer.Fail("MESSAGE_TOO_LARGE", "메시지가 너무 큽니다."); return; }
            } while (!received.EndOfMessage);

            var now = Environment.TickCount64;
            while (messageTimes.TryPeek(out var timestamp) && now - timestamp >= 1000) messageTimes.Dequeue();
            if (messageTimes.Count >= 20) { peer.Fail("RATE_LIMIT", "요청이 너무 빠릅니다."); return; }
            messageTimes.Enqueue(now);
            ClientCommand? command;
            try { command = JsonSerializer.Deserialize<ClientCommand>(buffer.AsSpan(0, length), Protocol.Json); }
            catch (JsonException) { peer.Fail("INVALID_MESSAGE", "메시지 형식을 확인해 주세요."); continue; }
            if (command is null) { peer.Fail("INVALID_MESSAGE", "메시지가 비어 있습니다."); continue; }
            try { await rooms.HandleAsync(peer, command).WaitAsync(cancellation); }
            catch (InvalidOperationException) { peer.Fail("SERVER_BUSY", "서버가 바쁩니다. 다시 연결해 주세요."); return; }
        }
    }

    private sealed class SocketPeer(WebSocket socket, CancellationTokenSource lifetime) : IRoomPeer
    {
        private readonly Channel<ServerEvent> _outgoing = Channel.CreateBounded<ServerEvent>(new BoundedChannelOptions(64)
        { SingleReader = true, SingleWriter = false, FullMode = BoundedChannelFullMode.Wait });
        public string Id { get; } = Guid.NewGuid().ToString("N");
        public bool Send(ServerEvent message)
        {
            if (_outgoing.Writer.TryWrite(message)) return true;
            socket.Abort();
            lifetime.Cancel();
            return false;
        }
        public void Fail(string code, string message) => Send(new ServerEvent("error") { Code = code, Message = message });
        public void Complete() => _outgoing.Writer.TryComplete();
        public async Task WriteAsync(CancellationToken cancellation)
        {
            try
            {
                await foreach (var message in _outgoing.Reader.ReadAllAsync(cancellation))
                    await socket.SendAsync(JsonSerializer.SerializeToUtf8Bytes(message, Protocol.Json), WebSocketMessageType.Text, true, cancellation);
            }
            catch (Exception error) when (error is WebSocketException or OperationCanceledException)
            {
                socket.Abort();
                await lifetime.CancelAsync();
            }
        }
    }
}
