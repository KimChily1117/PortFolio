using System.Diagnostics;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Kimchily.Server.Core;
using Kimchily.Server.Core.Jobs;

var checks = new List<string>();
var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
var evidence = Path.Combine(root, "Artifacts", "checks", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
Directory.CreateDirectory(evidence);
var hostLog = new StringBuilder();
Process? host = null;
var failed = false;
void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
void Passed(string name) { checks.Add(name); Console.WriteLine("PASS " + name); }
ClientCommand Join(string name = "칠리", string room = "test", string world = "demo", string revision = "r1") =>
    new() { ProtocolVersion = 1, Type = "join", Name = name, RoomId = room, WorldId = world, RevisionId = revision };
async Task Dispatch(RoomHub hub, FakePeer peer, ClientCommand command)
{
    var task = hub.HandleAsync(peer, command); hub.Flush(); await task;
}
try
{
    var order = new List<int>();
    long now = 0;
    var queue = new JobSerializer(clock: () => now);
    queue.TryPush(new Job<int>(order.Add, 1)); queue.TryPush(() => order.Add(2)); queue.Flush();
    Assert(order.SequenceEqual([1, 2]), "FIFO work order changed.");
    Passed("Reused job queue executes immediate work in FIFO order");
    order.Clear();
    queue.TryPushAfter(100, () => order.Add(3));
    queue.TryPushAfter(20, () => order.Add(1));
    queue.TryPushAfter(20, () => order.Add(2));
    now = 19; queue.Flush(); Assert(order.Count == 0, "Timer ran too early.");
    now = 20; queue.Flush(); Assert(order.SequenceEqual([1, 2]), "Short timer blocked behind a long timer.");
    now = 100; queue.Flush(); Assert(order.SequenceEqual([1, 2, 3]), "Long timer did not execute.");
    Passed("Timers execute earliest first, with stable ordering and no early execution");
    var bounded = new JobSerializer(2);
    Assert(bounded.TryPush(() => { }) && bounded.TryPushAfter(1000, () => { }) && !bounded.TryPush(() => { }), "Queue not bounded.");
    Passed("Immediate and delayed work share a bounded queue capacity");
    var concurrent = new JobSerializer();
    var active = 0; var maximum = 0; var executions = 0;
    for (var i = 0; i < 100; i++) concurrent.TryPush(() =>
    {
        var value = Interlocked.Increment(ref active); maximum = Math.Max(maximum, value);
        Thread.SpinWait(1000); Interlocked.Increment(ref executions); Interlocked.Decrement(ref active);
    });
    Parallel.For(0, 8, _ => concurrent.Flush());
    Assert(maximum == 1 && executions == 100, "Concurrent Flush overlapped jobs.");
    Passed("Concurrent drains never execute room work simultaneously");
    var bad = queue.InvokeAsync<int>(() => throw new InvalidOperationException("expected"));
    var good = queue.InvokeAsync(() => 7); queue.Flush();
    Assert(bad.IsFaulted && await good == 7, "Request exception blocked later jobs.");
    Passed("A failed queued request completes with an error without losing later requests");

    var hub = new RoomHub(() => now);
    var first = new FakePeer("a"); var second = new FakePeer("b");
    await Dispatch(hub, first, Join()); await Dispatch(hub, second, Join("친구"));
    Assert(second.Events.Single(e => e.Type == "joined").Players!.Length == 2 && first.Events.Last().Type == "playerJoined", "Initial membership not synchronized.");
    Passed("Two guests receive initial membership and room join events");
    await Dispatch(hub, first, new() { ProtocolVersion = 1, Type = "chat", Text = "안녕하세요 <script>hello</script>" });
    Assert(second.Events.Last().Chat is { PlayerId: "a", Name: "칠리", Text: "안녕하세요 <script>hello</script>" }, "Chat identity was not assigned by the server.");
    Passed("Unicode chat is relayed with server-owned sender identity");
    var stranger = new FakePeer("c");
    await Dispatch(hub, stranger, Join(revision: "r2"));
    Assert(stranger.Events.Last().Players!.Length == 1 && stranger.Events.Last().History!.Length == 0, "Revision isolation failed.");
    var otherWorld = new FakePeer("d"); await Dispatch(hub, otherWorld, Join(world: "other"));
    var otherRoom = new FakePeer("e"); await Dispatch(hub, otherRoom, Join(room: "other"));
    var isolatedCounts = new[] { stranger.Events.Count, otherWorld.Events.Count, otherRoom.Events.Count };
    await Dispatch(hub, first, new() { ProtocolVersion = 1, Type = "chat", Text = "private room message" });
    Assert(isolatedCounts.SequenceEqual([stranger.Events.Count, otherWorld.Events.Count, otherRoom.Events.Count]), "A chat crossed room boundaries.");
    Passed("World, revision, and room identifiers independently isolate membership and chat");
    var pose = new PlayerState(1, 1, 1, 1, 90, 4, true, 0);
    await Dispatch(hub, first, new() { ProtocolVersion = 1, Type = "state", State = pose });
    Assert(second.Events.Last().Player is { PlayerId: "a", State.X: 1, State.Yaw: 90 }, "Pose identity/state was not synchronized.");
    Assert(isolatedCounts.SequenceEqual([stranger.Events.Count, otherWorld.Events.Count, otherRoom.Events.Count]), "A pose crossed room boundaries.");
    Passed("Player position and facing are relayed with server-owned identity and room isolation");
    int poseEvents = second.Events.Count;
    await Dispatch(hub, first, new() { ProtocolVersion = 1, Type = "state", State = pose with { Sequence = 2, X = 2 } });
    now += 100;
    await Dispatch(hub, first, new() { ProtocolVersion = 1, Type = "state", State = pose with { Sequence = 0, X = 2 } });
    Assert(second.Events.Count == poseEvents, "Stale or too-frequent state was relayed.");
    await Dispatch(hub, first, new() { ProtocolVersion = 1, Type = "state", State = pose with { Sequence = 3, X = float.NaN } });
    Assert(first.Events.Last().Code == "INVALID_STATE", "Non-finite position accepted.");
    await Dispatch(hub, first, new() { ProtocolVersion = 1, Type = "state", State = pose with { Sequence = 3, X = 999 } });
    Assert(first.Events.Last().Code == "STATE_TOO_FAR", "Position teleport accepted.");
    Passed("Stale, overly frequent, non-finite and implausibly distant movement samples are rejected");
    now += 1000;
    await Dispatch(hub, first, new() { ProtocolVersion = 1, Type = "state", State = pose with { Sequence = 4, Y = -25, Grounded = false } });
    now += 100;
    await Dispatch(hub, first, new() { ProtocolVersion = 1, Type = "state", State = pose with { Sequence = 5 } });
    Assert(second.Events.Last().Player?.State is { Sequence: 5, Y: 1 }, "Respawn did not return to the original spawn.");
    Passed("A fallen player may respawn at its initially recorded position");
    var late = new FakePeer("late"); await Dispatch(hub, late, Join());
    Assert(late.Events.Last().History!.Length == 2, "Late joiner did not receive recent history.");
    Assert(late.Events.Last().Players!.Single(p => p.PlayerId == "a").State is { Sequence: 5, X: 1 }, "Late joiner missed latest avatar pose.");
    Passed("Late joiners receive current members and bounded recent chat history");
    var unjoined = new FakePeer("unjoined");
    await Dispatch(hub, unjoined, new() { ProtocolVersion = 1, Type = "chat", Text = "no room" });
    Assert(unjoined.Events.Last().Code == "NOT_JOINED", "Unjoined chat accepted.");
    await Dispatch(hub, unjoined, Join() with { ProtocolVersion = 99 });
    Assert(unjoined.Events.Last().Code == "PROTOCOL_MISMATCH", "Wrong protocol accepted.");
    await Dispatch(hub, unjoined, Join() with { RoomId = "../escape" });
    Assert(unjoined.Events.Last().Code == "INVALID_ROOM", "Invalid room id accepted.");
    await Dispatch(hub, unjoined, Join() with { Name = "bad\nname" });
    Assert(unjoined.Events.Last().Code == "INVALID_NAME", "Control characters accepted.");
    Passed("Unjoined actions, wrong protocol, invalid room ids, and invalid names are rejected");
    await Dispatch(hub, first, new() { ProtocolVersion = 1, Type = "chat", Text = new string('a', 301) });
    Assert(first.Events.Last().Code == "INVALID_CHAT", "Oversized chat accepted.");
    for (var i = 0; i < 4; i++) await Dispatch(hub, first, new() { ProtocolVersion = 1, Type = "chat", Text = "rate check" });
    Assert(first.Events.Last().Code == "CHAT_RATE_LIMIT", "Chat rate limit missing.");
    now += 5000; await Dispatch(hub, first, new() { ProtocolVersion = 1, Type = "chat", Text = "after cooldown" });
    Assert(first.Events.Last().Type == "chat", "Chat rate limit never recovers.");
    Passed("Chat size and rate limits reject abuse and recover after cooldown");
    for (var i = 0; i < 25; i++)
    { now += 5000; await Dispatch(hub, first, new() { ProtocolVersion = 1, Type = "chat", Text = "history " + i }); }
    var historyPeer = new FakePeer("history"); await Dispatch(hub, historyPeer, Join());
    Assert(historyPeer.Events.Last().History is { Length: 20 } history && history[0].Text == "history 5", "History is unbounded or not oldest-first.");
    Passed("Only the latest twenty chat messages are retained per room");
    var fullHub = new RoomHub();
    for (var i = 0; i < 8; i++) await Dispatch(fullHub, new FakePeer("full-" + i), Join());
    var ninth = new FakePeer("ninth"); await Dispatch(fullHub, ninth, Join());
    Assert(ninth.Events.Last().Code == "ROOM_FULL", "Ninth player entered an eight-player room.");
    Passed("Room capacity is enforced by the server");
    var smallHub = new RoomHub(); var sole = new FakePeer("sole");
    await Dispatch(smallHub, sole, Join());
    var disconnected = smallHub.DisconnectAsync(sole); smallHub.Flush(); await disconnected;
    var snapshot = smallHub.SnapshotAsync(); smallHub.Flush(); Assert((await snapshot).Length == 0, "Empty room was not removed.");
    await Dispatch(smallHub, sole, Join()); Assert(sole.Events.Last().History!.Length == 0, "Removed room retained stale history.");
    Passed("Disconnect removes membership; the last departure removes room state");

    // Start only this test's own host on an OS-assigned loopback port.
    var listening = new TaskCompletionSource<Uri>(TaskCreationOptions.RunContinuationsAsynchronously);
    var info = new ProcessStartInfo(Environment.ProcessPath!)
    {
        UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
        RedirectStandardOutput = true, RedirectStandardError = true,
        WorkingDirectory = root
    };
    info.ArgumentList.Add(Path.Combine(root, "src/Kimchily.Server.Host/bin/Release/net10.0/Kimchily.Server.Host.dll"));
    info.ArgumentList.Add("--urls"); info.ArgumentList.Add("http://127.0.0.1:0");
    info.ArgumentList.Add("--Realtime:AllowedOrigins:0"); info.ArgumentList.Add("http://127.0.0.1:8788");
    host = new Process { StartInfo = info, EnableRaisingEvents = true };
    void Capture(object sender, DataReceivedEventArgs eventArgs)
    {
        if (eventArgs.Data is not { } line) return;
        lock (hostLog) hostLog.AppendLine(line);
        var match = Regex.Match(line, @"Now listening on: (http://127\.0\.0\.1:\d+)");
        if (match.Success) listening.TrySetResult(new Uri(match.Groups[1].Value));
    }
    host.OutputDataReceived += Capture; host.ErrorDataReceived += Capture;
    host.Start(); host.BeginOutputReadLine(); host.BeginErrorReadLine();
    var address = await listening.Task.WaitAsync(TimeSpan.FromSeconds(20));
    using var http = new HttpClient { BaseAddress = address, Timeout = TimeSpan.FromSeconds(5) };
    using var health = JsonDocument.Parse(await http.GetStringAsync("/health"));
    Assert(health.RootElement.GetProperty("service").GetString() == "kimchily-realtime", "Wrong health service.");
    var html = await http.GetStringAsync("/");
    Assert(html.Contains("join-form") && (await http.GetStringAsync("/app.js")).Contains("textContent"), "Demo assets missing from build output.");
    Assert((await http.GetAsync("/ws")).StatusCode == HttpStatusCode.BadRequest, "Non-WebSocket request accepted.");
    Passed("Real Kestrel host serves health and browser demo; rejects plain HTTP at /ws");
    var wsAddress = new UriBuilder(address) { Scheme = "ws", Path = "/ws" }.Uri;
    await using var alice = await Wire.Connect(wsAddress);
    await using var bob = await Wire.Connect(wsAddress);
    await alice.Send(Join("앨리스")); var aliceJoin = await alice.Expect("joined");
    await bob.Send(Join("밥")); var bobJoin = await bob.Expect("joined"); await alice.Expect("playerJoined");
    Assert(bobJoin.Players!.Length == 2 && aliceJoin.SelfId != bobJoin.SelfId, "Real clients did not share a room.");
    await alice.Send(new ClientCommand { ProtocolVersion = 1, Type = "chat", Text = "휴대폰 연결 테스트" });
    var fromAlice = await alice.Expect("chat"); var toBob = await bob.Expect("chat");
    Assert(fromAlice.Chat!.Id == toBob.Chat!.Id && toBob.Chat.PlayerId == aliceJoin.SelfId, "Real chat was not broadcast consistently.");
    Passed("Two real WebSocket clients join and exchange the same server-identified chat");
    await alice.Send(new ClientCommand { ProtocolVersion = 1, Type = "state", State = pose });
    Assert((await bob.Expect("state")).Player is { State.X: 1, State.Yaw: 90 }, "Real pose delivery failed.");
    Passed("Real WebSocket clients receive avatar position and rotation samples");
    await alice.Raw("{invalid"); Assert((await alice.Expect("error")).Code == "INVALID_MESSAGE", "Malformed JSON accepted.");
    await alice.Raw("{\"protocolVersion\":1,\"type\":\"chat\",\"text\":\"spoof\",\"playerId\":\"other\"}");
    Assert((await alice.Expect("error")).Code == "INVALID_MESSAGE", "Spoofed sender field accepted.");
    await alice.Send(new ClientCommand { ProtocolVersion = 1, Type = "ping" }); await alice.Expect("pong");
    Passed("Malformed JSON and spoofed sender fields are rejected without corrupting the connection");
    await alice.Fragmented("{\"protocolVersion\":1,\"type\":\"chat\",\"text\":\"fragmented\"}");
    Assert((await bob.Expect("chat")).Chat!.Text == "fragmented", "Fragmented text not reassembled."); await alice.Expect("chat");
    Passed("WebSocket fragments are reassembled into one validated message");
    await using (var lateWire = await Wire.Connect(wsAddress))
    {
        await lateWire.Send(Join("늦은 입장")); var state = await lateWire.Expect("joined");
        Assert(state.Players!.Length == 3 && state.History!.Length == 2, "Late network join had incomplete state.");
        await alice.Expect("playerJoined"); await bob.Expect("playerJoined");
    }
    await alice.Expect("playerLeft"); await bob.Expect("playerLeft");
    Passed("A late network join receives a snapshot; socket close notifies remaining members");
    await bob.Send(new ClientCommand { ProtocolVersion = 1, Type = "leave" }); await bob.Expect("left"); await alice.Expect("playerLeft");
    await bob.Send(Join("밥")); Assert((await bob.Expect("joined")).Players!.Length == 2, "Leave/rejoin failed."); await alice.Expect("playerJoined");
    Passed("Explicit leave and fresh rejoin restore current membership");
    using (var forbidden = new ClientWebSocket())
    {
        forbidden.Options.SetRequestHeader("Origin", "https://untrusted.example");
        var rejected = false;
        try { await forbidden.ConnectAsync(wsAddress, CancellationToken.None); } catch (WebSocketException) { rejected = true; }
        Assert(rejected, "Cross-origin browser handshake accepted.");
    }
    await using (var allowed = await Wire.Connect(wsAddress, address.GetLeftPart(UriPartial.Authority))) { }
    Passed("Same-origin browser handshakes succeed and foreign origins are rejected");
    await using (var unityOrigin = await Wire.Connect(wsAddress, "http://127.0.0.1:8788")) { }
    Passed("The explicitly configured Unity WebGL origin can connect across ports");
    await using (var oversized = await Wire.Connect(wsAddress))
    {
        await oversized.Raw(new string('x', 4097));
        Assert((await oversized.Expect("error")).Code == "MESSAGE_TOO_LARGE", "Oversized frame accepted.");
    }
    await using (var binary = await Wire.Connect(wsAddress))
    {
        await binary.Binary(); Assert((await binary.Expect("error")).Code == "TEXT_REQUIRED", "Binary message accepted.");
    }
    Passed("Oversized and binary WebSocket messages are rejected");
    await using (var flood = await Wire.Connect(wsAddress))
    {
        for (var i = 0; i < 21; i++) await flood.Send(new ClientCommand { ProtocolVersion = 1, Type = "ping" });
        ServerEvent response;
        do { response = await flood.Next(); } while (response.Type == "pong");
        Assert(response.Code == "RATE_LIMIT", "Connection message flood was not limited.");
    }
    Passed("Connection-wide message floods are bounded independently of chat limits");
}
catch (Exception error)
{
    failed = true;
    Console.Error.WriteLine(error);
}
finally
{
    if (host is not null)
    {
        if (!host.HasExited) { host.Kill(entireProcessTree: true); await host.WaitForExitAsync(); }
        host.Dispose();
    }
    lock (hostLog) File.WriteAllText(Path.Combine(evidence, "host.log"), hostLog.ToString());
    File.WriteAllText(Path.Combine(evidence, "results.json"), JsonSerializer.Serialize(new { passed = checks.Count, failed = failed ? 1 : 0, checks }, new JsonSerializerOptions { WriteIndented = true }));
}
Console.WriteLine($"Checks: {checks.Count} passed, {(failed ? 1 : 0)} failed. Evidence: {evidence}");
return failed ? 1 : 0;

sealed class FakePeer(string id) : IRoomPeer
{
    public string Id { get; } = id;
    public List<ServerEvent> Events { get; } = [];
    public bool Send(ServerEvent message) { Events.Add(message); return true; }
}

sealed class Wire : IAsyncDisposable
{
    private readonly ClientWebSocket _socket = new();
    public static async Task<Wire> Connect(Uri address, string? origin = null)
    {
        var wire = new Wire();
        if (origin is not null) wire._socket.Options.SetRequestHeader("Origin", origin);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await wire._socket.ConnectAsync(address, timeout.Token);
        await wire.Expect("hello");
        return wire;
    }
    public Task Send(ClientCommand command) => Raw(JsonSerializer.Serialize(command, Protocol.Json));
    public async Task Raw(string text)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _socket.SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, true, timeout.Token);
    }
    public async Task Fragmented(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _socket.SendAsync(new ArraySegment<byte>(bytes, 0, 10), WebSocketMessageType.Text, false, timeout.Token);
        await _socket.SendAsync(new ArraySegment<byte>(bytes, 10, bytes.Length - 10), WebSocketMessageType.Text, true, timeout.Token);
    }
    public async Task Binary()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _socket.SendAsync(new byte[] { 1 }, WebSocketMessageType.Binary, true, timeout.Token);
    }
    public async Task<ServerEvent> Next()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var message = new MemoryStream();
        var buffer = new byte[16384];
        WebSocketReceiveResult result;
        do
        {
            result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), timeout.Token);
            if (result.MessageType == WebSocketMessageType.Close) throw new InvalidOperationException("Unexpected socket close.");
            message.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);
        return JsonSerializer.Deserialize<ServerEvent>(message.ToArray(), Protocol.Json)!;
    }
    public async Task<ServerEvent> Expect(string type)
    {
        var result = await Next();
        if (result.Type != type) throw new InvalidOperationException($"Expected {type}, received {result.Type}: {result.Code}");
        return result;
    }
    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_socket.State == WebSocketState.Open)
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Test completed", timeout.Token);
            }
        }
        catch (Exception error) when (error is WebSocketException or OperationCanceledException) { }
        finally { _socket.Dispose(); }
    }
}
