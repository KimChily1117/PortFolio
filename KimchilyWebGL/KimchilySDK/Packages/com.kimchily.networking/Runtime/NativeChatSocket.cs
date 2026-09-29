#if !UNITY_WEBGL || UNITY_EDITOR
using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Kimchily.Networking
{
    // No Unity APIs run on the receive worker. The owner drains events in Update.
    internal sealed class NativeChatSocket : IDisposable
    {
        readonly ClientWebSocket socket = new ClientWebSocket();
        readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        readonly SemaphoreSlim sendLock = new SemaphoreSlim(1);
        readonly ConcurrentQueue<ChatWireEvent> events;
        readonly int generation;
        int pendingSends;
        public NativeChatSocket(string endpoint, int generation, ConcurrentQueue<ChatWireEvent> events)
        {
            this.generation = generation; this.events = events;
            _ = Run(endpoint);
        }
        void Report(string type, string data = "") => events.Enqueue(new ChatWireEvent { generation = generation, type = type, data = data });
        async Task Run(string endpoint)
        {
            try
            {
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token))
                {
                    timeout.CancelAfter(10000);
                    await socket.ConnectAsync(new Uri(endpoint), timeout.Token).ConfigureAwait(false);
                }
                Report("open");
                var buffer = new byte[65536];
                while (!lifetime.IsCancellationRequested)
                {
                    int count = 0;
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer, count, buffer.Length - count), lifetime.Token).ConfigureAwait(false);
                        if (result.MessageType == WebSocketMessageType.Close) { Report("closed"); return; }
                        if (result.MessageType != WebSocketMessageType.Text) throw new InvalidOperationException("Unexpected server message.");
                        count += result.Count;
                        if (count == buffer.Length && !result.EndOfMessage) throw new InvalidOperationException("Server message too large.");
                    } while (!result.EndOfMessage);
                    if (events.Count >= 128) throw new InvalidOperationException("Client message queue full.");
                    Report("message", Encoding.UTF8.GetString(buffer, 0, count));
                }
            }
            catch (Exception) { if (!lifetime.IsCancellationRequested) Report("closed", "서버 연결이 끊겼습니다. 주소를 확인하고 다시 연결하세요."); }
        }
        public async void Send(string json)
        {
            if (Interlocked.Increment(ref pendingSends) > 16) { Interlocked.Decrement(ref pendingSends); return; }
            bool locked = false;
            try
            {
                await sendLock.WaitAsync(lifetime.Token).ConfigureAwait(false); locked = true;
                byte[] bytes = Encoding.UTF8.GetBytes(json);
                await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, lifetime.Token).ConfigureAwait(false);
            }
            catch (Exception) { if (!lifetime.IsCancellationRequested) Report("closed", "메시지를 보내지 못했습니다. 다시 연결하세요."); }
            finally { if (locked) sendLock.Release(); Interlocked.Decrement(ref pendingSends); }
        }
        public void Dispose() { lifetime.Cancel(); socket.Abort(); socket.Dispose(); }
    }
}
#endif
