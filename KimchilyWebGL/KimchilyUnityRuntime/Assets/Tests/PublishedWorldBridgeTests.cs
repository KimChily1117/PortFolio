using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Kimchily.World.Tests
{
    /// <summary>Exercises the real UnityWebRequest/host boundary against an owned loopback server.</summary>
    public sealed class PublishedWorldBridgeTests
    {
        private const float TimeoutSeconds = 30;
        private const string RemoteWorld = "published-test";
        private const string RemoteRevision = "r1";
        private static readonly string WrongHash = new string('0', 64);
        private readonly List<HostEvent> events = new List<HostEvent>();
        private GameObject host;
        private KimchilyHostBridge bridge;
        private LoopbackResponse server;
        private HashSet<string> originalStaging;
        private HashSet<Scene> originalScenes;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            KimchilyHostBridge existing = KimchilyHostBridge.Instance;
            if (existing != null)
            {
                existing.Receive(JsonUtility.ToJson(Close("setup-close")));
                yield return WaitUntil(() => existing.State == WorldRuntimeState.Idle, "previous bridge close");
                UnityEngine.Object.Destroy(existing.gameObject);
                yield return null;
            }
            Scene leftover = SceneManager.GetSceneByName(HostProtocol.DemoSceneName);
            if (leftover.IsValid() && leftover.isLoaded) yield return SceneManager.UnloadSceneAsync(leftover);
            events.Clear();
            host = new GameObject("Published world test host");
            bridge = host.AddComponent<KimchilyHostBridge>();
            bridge.EventRaised += Capture;
            yield return null;
            events.Clear();
            originalStaging = StagingDirectories();
            originalScenes = LoadedSceneHandles();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            try
            {
                if (bridge != null && bridge.State != WorldRuntimeState.Idle)
                {
                    Send(Close("teardown-close"));
                    yield return WaitUntil(() => bridge.State == WorldRuntimeState.Idle, "test download/scene cleanup");
                }
            }
            finally
            {
                if (bridge != null) bridge.EventRaised -= Capture;
                server?.Dispose();
                server = null;
                if (host != null) UnityEngine.Object.Destroy(host);
            }
            yield return null;
            Assert.That(KimchilyHostBridge.Instance, Is.Null);
            AssertNoNewStaging();
        }

        [UnityTest]
        public IEnumerator InvalidRemoteUrlsAndHashesNeverOpenASceneOrCreateStaging()
        {
            var commands = new[]
            {
                Remote("bad-scheme", "file:///worlds/published-test/r1/world.json", WrongHash),
                Remote("bad-path", "http://127.0.0.1:1/worlds/another/r1/world.json", WrongHash),
                Remote("bad-userinfo", "http://user:password@127.0.0.1:1/worlds/published-test/r1/world.json", WrongHash),
                Remote("missing-hash", "http://127.0.0.1:1/worlds/published-test/r1/world.json", null),
                Remote("invalid-hash", "http://127.0.0.1:1/worlds/published-test/r1/world.json", new string('g', 64))
            };
            foreach (HostCommand command in commands)
            {
                Send(command);
                yield return WaitForEvent("WorldFailed", command.requestId);
                Assert.That(Event("WorldFailed", command.requestId).code, Is.EqualTo("LOAD_FAILED"));
                Assert.That(bridge.State, Is.EqualTo(WorldRuntimeState.Idle));
                AssertNoNewStaging();
                AssertNoNewScene();
            }
            Assert.That(events.Any(item => item.type == "WorldReady"), Is.False);
        }

        [UnityTest]
        public IEnumerator CloseDuringActualDownloadCancelsBeforeAcknowledgementAndDeletesStaging()
        {
            server = new LoopbackResponse("{}", holdResponse: true);
            Send(Remote("remote-cancelled", server.ManifestUrl, WrongHash));
            yield return WaitUntil(() => server.RequestReceived, "loopback manifest request");
            Assert.That(server.Failure, Is.Null);
            Assert.That(server.RequestPath, Is.EqualTo("/worlds/published-test/r1/world.json"));
            Assert.That(bridge.State, Is.EqualTo(WorldRuntimeState.Loading));
            Assert.That(StagingDirectories().Except(originalStaging).Count(), Is.EqualTo(1));

            Send(Close("close-downloading"));
            yield return WaitForEvent("WorldClosed", "close-downloading");
            Assert.That(Event("WorldFailed", "remote-cancelled").code, Is.EqualTo("CANCELLED"));
            Assert.That(events.FindIndex(item => item.type == "WorldFailed" && item.requestId == "remote-cancelled"),
                Is.LessThan(events.FindIndex(item => item.type == "WorldClosed" && item.requestId == "close-downloading")));
            Assert.That(bridge.State, Is.EqualTo(WorldRuntimeState.Idle));
            Assert.That(bridge.CurrentWorldId, Is.Empty);
            AssertNoNewStaging();
            AssertNoNewScene();
            server.ReleaseResponse();
            yield return null;
            yield return null;
            Assert.That(events.Any(item => item.type == "WorldReady"), Is.False);
            Assert.That(events.Count(item => item.type == "WorldFailed" && item.requestId == "remote-cancelled"), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ManifestHashMismatchFailsCleansStagingAndAllowsDemoRetry()
        {
            server = new LoopbackResponse("{\"schemaVersion\":1,\"worldId\":\"published-test\",\"revisionId\":\"r1\"}", holdResponse: false);
            Send(Remote("bad-manifest-hash", server.ManifestUrl, WrongHash));
            yield return WaitForEvent("WorldFailed", "bad-manifest-hash");
            HostEvent failure = Event("WorldFailed", "bad-manifest-hash");
            Assert.That(server.RequestReceived, Is.True);
            Assert.That(server.Failure, Is.Null);
            Assert.That(failure.code, Is.EqualTo("LOAD_FAILED"));
            Assert.That(failure.message, Does.Contain("manifest does not match"));
            Assert.That(bridge.State, Is.EqualTo(WorldRuntimeState.Idle));
            Assert.That(events.Any(item => item.type == "WorldReady"), Is.False);
            AssertNoNewStaging();
            AssertNoNewScene();

            Send(new HostCommand
            {
                protocolVersion = HostProtocol.Version, type = "OpenWorld", requestId = "demo-after-remote-failure",
                worldId = HostProtocol.DemoWorldId, revisionId = HostProtocol.DemoRevisionId
            });
            yield return WaitForEvent("WorldReady", "demo-after-remote-failure");
            Assert.That(bridge.State, Is.EqualTo(WorldRuntimeState.Ready));
            Assert.That(SceneManager.GetSceneByName(HostProtocol.DemoSceneName).isLoaded, Is.True);
            Send(Close("close-retry-demo"));
            yield return WaitForEvent("WorldClosed", "close-retry-demo");
            AssertNoNewStaging();
            AssertNoNewScene();
        }

        private static HostCommand Remote(string request, string url, string hash) => new HostCommand
        {
            protocolVersion = HostProtocol.Version, type = "OpenWorld", requestId = request,
            worldId = RemoteWorld, revisionId = RemoteRevision, manifestUrl = url, manifestSha256 = hash
        };
        private static HostCommand Close(string request) => new HostCommand
        {
            protocolVersion = HostProtocol.Version, type = "CloseWorld", requestId = request
        };
        private void Send(HostCommand command) => bridge.Receive(JsonUtility.ToJson(command));
        private void Capture(HostEvent value) => events.Add(value);
        private HostEvent Event(string type, string request) => events.Single(item => item.type == type && item.requestId == request);
        private IEnumerator WaitForEvent(string type, string request) =>
            WaitUntil(() => events.Any(item => item.type == type && item.requestId == request), type + " for " + request);

        private static IEnumerator WaitUntil(Func<bool> condition, string description)
        {
            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, "Timed out waiting for " + description);
        }

        private static HashSet<string> StagingDirectories()
        {
            string root = Path.Combine(Application.temporaryCachePath, "KimchilyWorldDownloads");
            return new HashSet<string>(Directory.Exists(root) ? Directory.GetDirectories(root) : Array.Empty<string>());
        }
        private static HashSet<Scene> LoadedSceneHandles() => new HashSet<Scene>(Enumerable.Range(0, SceneManager.sceneCount)
            .Select(SceneManager.GetSceneAt).Where(scene => scene.isLoaded));
        private void AssertNoNewStaging() => Assert.That(StagingDirectories().Except(originalStaging), Is.Empty,
            "A terminal host response must follow deletion of this request's download staging directory.");
        private void AssertNoNewScene() => Assert.That(LoadedSceneHandles().Except(originalScenes), Is.Empty,
            "A rejected/cancelled remote request must not leave an additive scene loaded.");

        /// <summary>
        /// One HTTP response on an OS-assigned loopback port. TcpListener avoids Windows
        /// HttpListener URLACL/admin requirements. Worker code never calls Unity APIs.
        /// </summary>
        private sealed class LoopbackResponse : IDisposable
        {
            private readonly TcpListener listener;
            private readonly TaskCompletionSource<bool> release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly byte[] body;
            private TcpClient client;
            private int received;
            private volatile bool disposed;
            private readonly Task completion;
            public string ManifestUrl { get; }
            public string RequestPath { get; private set; }
            public Exception Failure { get; private set; }
            public bool RequestReceived => Volatile.Read(ref received) != 0;

            public LoopbackResponse(string json, bool holdResponse)
            {
                body = Encoding.UTF8.GetBytes(json);
                listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start(1);
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                ManifestUrl = "http://127.0.0.1:" + port + "/worlds/published-test/r1/world.json";
                if (!holdResponse) release.TrySetResult(true);
                completion = Respond();
            }

            public void ReleaseResponse() => release.TrySetResult(true);

            private async Task Respond()
            {
                try
                {
                    client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                    using (client)
                    using (NetworkStream stream = client.GetStream())
                    {
                        var buffer = new byte[4096];
                        int count = 0;
                        string request = string.Empty;
                        while (!request.Contains("\r\n\r\n"))
                        {
                            int read = await stream.ReadAsync(buffer, count, buffer.Length - count).ConfigureAwait(false);
                            if (read == 0) throw new IOException("Client closed before HTTP headers.");
                            count += read;
                            request = Encoding.ASCII.GetString(buffer, 0, count);
                            if (count == buffer.Length && !request.Contains("\r\n\r\n"))
                                throw new IOException("Test request headers exceed 4096 bytes.");
                        }
                        RequestPath = request.Split(' ')[1];
                        Volatile.Write(ref received, 1);
                        if (!await release.Task.ConfigureAwait(false)) return;
                        byte[] header = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: " + body.Length + "\r\nConnection: close\r\n\r\n");
                        await stream.WriteAsync(header, 0, header.Length).ConfigureAwait(false);
                        await stream.WriteAsync(body, 0, body.Length).ConfigureAwait(false);
                    }
                }
                catch (Exception exception)
                {
                    // Request cancellation deliberately closes the socket before a delayed reply.
                    if (!disposed && !(RequestReceived && exception is IOException)) Failure = exception;
                }
            }

            public void Dispose()
            {
                disposed = true;
                release.TrySetResult(false);
                listener.Stop();
                client?.Close();
                // Respond catches its exceptions; do not block Unity's main thread on network work.
                GC.KeepAlive(completion);
            }
        }
    }
}
