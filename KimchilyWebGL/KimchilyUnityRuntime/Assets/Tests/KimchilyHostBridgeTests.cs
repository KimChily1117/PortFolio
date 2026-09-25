using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Kimchily.Creator;
using Kimchily.Creator.Mobile;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Kimchily.World.Tests
{
    public sealed class KimchilyHostBridgeTests
    {
        const float TimeoutSeconds = 30;
        readonly List<HostEvent> events = new List<HostEvent>();
        GameObject host;
        KimchilyHostBridge bridge;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // The generated Bootstrap scene may have instantiated a bridge before
            // Test Runner starts. End that dedicated test session before isolating
            // each case; never leave a previous additive demo or singleton behind.
            KimchilyHostBridge existing = KimchilyHostBridge.Instance;
            if (existing != null)
            {
                existing.Receive(JsonUtility.ToJson(Command("CloseWorld", "setup-close")));
                yield return WaitUntil(() => existing.State == WorldRuntimeState.Idle, "existing world to close");
                UnityEngine.Object.Destroy(existing.gameObject);
                yield return null;
            }
            Scene leftover = SceneManager.GetSceneByName(HostProtocol.DemoSceneName);
            if (leftover.IsValid() && leftover.isLoaded)
                yield return SceneManager.UnloadSceneAsync(leftover);

            events.Clear();
            host = new GameObject("Host bridge test");
            bridge = host.AddComponent<KimchilyHostBridge>();
            bridge.EventRaised += Capture;
            yield return null;
            Assert.AreSame(bridge, KimchilyHostBridge.Instance);
            Assert.AreEqual(HostProtocol.BridgeObjectName, host.name);
            Assert.IsTrue(events.Any(item => item.type == "RuntimeReady"));
            events.Clear();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (bridge != null)
            {
                if (bridge.State != WorldRuntimeState.Idle)
                {
                    Send(Command("CloseWorld", "teardown-close"));
                    yield return WaitUntil(() => bridge.State == WorldRuntimeState.Idle, "teardown world close");
                }
                bridge.EventRaised -= Capture;
            }
            if (host != null) UnityEngine.Object.Destroy(host);
            yield return null;
            Assert.IsNull(KimchilyHostBridge.Instance);
            AssertWorldUnloaded();
        }

        [UnityTest]
        public IEnumerator WorldReadyWaitsForScriptStartInsteadOfOnlySceneLoad()
        {
            ScriptStartupProbe probe = null;
            void Loaded(Scene scene, LoadSceneMode mode)
            {
                if (scene.name != HostProtocol.DemoSceneName) return;
                probe = scene.GetRootGameObjects()[0].AddComponent<ScriptStartupProbe>();
            }
            SceneManager.sceneLoaded += Loaded;
            try
            {
                Send(Command("OpenWorld", "script-start"));
                yield return WaitUntil(() => probe != null, "script probe scene load");
                yield return null;
                yield return null;
                Assert.AreEqual(WorldRuntimeState.Loading, bridge.State);
                Assert.IsFalse(events.Any(item => item.type == "WorldReady"));
                probe.HasStarted = true;
                yield return WaitForEvent("WorldReady", "script-start");
            }
            finally { SceneManager.sceneLoaded -= Loaded; }
        }

        [UnityTest]
        public IEnumerator ScriptStartupFailureReportsFailureAndUnloadsBeforeRetry()
        {
            void Loaded(Scene scene, LoadSceneMode mode)
            {
                if (scene.name != HostProtocol.DemoSceneName) return;
                var probe = scene.GetRootGameObjects()[0].AddComponent<ScriptStartupProbe>();
                probe.HasStarted = true;
                probe.IsFaulted = true;
                probe.LastError = "Invalid TypeScript constructor";
            }
            SceneManager.sceneLoaded += Loaded;
            try
            {
                Send(Command("OpenWorld", "script-failure"));
                yield return WaitForEvent("WorldFailed", "script-failure");
                Assert.AreEqual("SCRIPT_INITIALIZATION_FAILED", Event("WorldFailed", "script-failure").code);
                Assert.IsFalse(events.Any(item => item.type == "WorldReady"));
                yield return WaitUntil(() => bridge.State == WorldRuntimeState.Idle, "failed scene cleanup");
                AssertWorldUnloaded();
            }
            finally { SceneManager.sceneLoaded -= Loaded; }
            Send(Command("OpenWorld", "script-retry"));
            yield return WaitForEvent("WorldReady", "script-retry");
        }

        [UnityTest]
        public IEnumerator InitializeCanBeRequestedAgainWithMatchingRequestIds()
        {
            Send(Command("Initialize", "initialize-cold"));
            yield return WaitForEvent("RuntimeReady", "initialize-cold");
            Send(Command("Initialize", "initialize-warm"));
            yield return WaitForEvent("RuntimeReady", "initialize-warm");

            Assert.AreEqual(2, events.Count(item => item.type == "RuntimeReady"));
            Assert.IsTrue(events.All(item => item.protocolVersion == HostProtocol.Version));
            Assert.AreEqual(WorldRuntimeState.Idle, bridge.State);
        }

        [UnityTest]
        public IEnumerator InvalidJsonProtocolWorldAndRevisionFailWithoutOpeningAScene()
        {
            bridge.Receive("{invalid JSON");
            yield return WaitUntil(() => events.Any(item => item.code == "INVALID_JSON"), "invalid JSON failure");

            var wrongVersion = Command("OpenWorld", "wrong-version");
            wrongVersion.protocolVersion = 999;
            Send(wrongVersion);
            var wrongWorld = Command("OpenWorld", "wrong-world");
            wrongWorld.worldId = "unpublished-world";
            Send(wrongWorld);
            var wrongRevision = Command("OpenWorld", "wrong-revision");
            wrongRevision.revisionId = "unpublished-revision";
            Send(wrongRevision);
            Send(Command("UnsupportedCommand", "wrong-command"));
            Send(Command("OpenWorld", string.Empty));

            yield return WaitForEvent("WorldFailed", "wrong-command");
            Assert.AreEqual("UNSUPPORTED_PROTOCOL", Event("WorldFailed", "wrong-version").code);
            Assert.AreEqual("UNKNOWN_WORLD", Event("WorldFailed", "wrong-world").code);
            Assert.AreEqual("UNKNOWN_REVISION", Event("WorldFailed", "wrong-revision").code);
            Assert.AreEqual("UNKNOWN_COMMAND", Event("WorldFailed", "wrong-command").code);
            Assert.IsTrue(events.Any(item => item.code == "INVALID_REQUEST"));
            Assert.AreEqual(WorldRuntimeState.Idle, bridge.State);
            Assert.IsFalse(events.Any(item => item.type == "WorldReady"));
            AssertWorldUnloaded();
        }

        [UnityTest]
        public IEnumerator DemoOpensClosesCleansCoroutinesAndCanBeEnteredAgain()
        {
            for (int iteration = 0; iteration < 2; iteration++)
            {
                string openId = "open-" + iteration;
                string closeId = "close-" + iteration;
                Send(Command("OpenWorld", openId));
                yield return WaitForEvent("WorldReady", openId);
                // Start methods of newly activated scene behaviours must have run.
                yield return null;
                Assert.AreEqual(WorldRuntimeState.Ready, bridge.State);
                Assert.AreEqual(openId, bridge.CurrentOpenRequestId);
                Assert.AreEqual(HostProtocol.DemoWorldId, Event("WorldReady", openId).worldId);
                Assert.AreEqual(HostProtocol.DemoRevisionId, Event("WorldReady", openId).revisionId);
                Assert.AreEqual(1, Event("WorldReady", openId).progress);

                Scene loaded = SceneManager.GetSceneByName(HostProtocol.DemoSceneName);
                Assert.IsTrue(loaded.IsValid() && loaded.isLoaded);
                var players = loaded.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<KimchilyMobilePlayer>(true)).ToArray();
                Assert.AreEqual(1, players.Length, "Each world entry creates one controllable player.");
                CoroutineScheduler[] schedulers = loaded.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<CoroutineScheduler>(true)).ToArray();
                Assert.Greater(schedulers.Length, 0, "The demo should exercise the SDK scheduler.");
                Assert.Greater(schedulers.Sum(item => item.ActiveCount), 0);
                bool disposed = false;
                IEnumerator LongWork()
                {
                    try { yield return new WaitForSecondsRealtime(60); }
                    finally { disposed = true; }
                }
                CoroutineHandle handle = schedulers[0].StartRoutine(LongWork(), schedulers[0].gameObject);
                Send(Command("CloseWorld", closeId));
                yield return WaitForEvent("WorldClosed", closeId);

                Assert.IsTrue(disposed, "WorldClosed must follow explicit coroutine cleanup.");
                Assert.AreEqual(CoroutineStatus.Cancelled, handle.Status);
                Assert.AreEqual(WorldRuntimeState.Idle, bridge.State);
                Assert.AreEqual(string.Empty, bridge.CurrentWorldId);
                Assert.IsTrue(schedulers.All(item => item == null), "WorldClosed must follow scene unload.");
                Assert.IsTrue(players.All(item => item == null), "The local player must unload with its world.");
                AssertWorldUnloaded();
            }
        }

        [UnityTest]
        public IEnumerator CloseDuringLoadWaitsForCleanupAndNeverReportsWorldReady()
        {
            Send(Command("OpenWorld", "open-cancelled"));
            Send(Command("CloseWorld", "close-loading"));
            yield return WaitForEvent("WorldClosed", "close-loading");

            Assert.AreEqual("CANCELLED", Event("WorldFailed", "open-cancelled").code);
            Assert.IsFalse(events.Any(item => item.type == "WorldReady" && item.requestId == "open-cancelled"));
            Assert.Less(events.FindIndex(item => item.type == "WorldFailed" && item.requestId == "open-cancelled"),
                events.FindIndex(item => item.type == "WorldClosed" && item.requestId == "close-loading"));
            Assert.AreEqual(WorldRuntimeState.Idle, bridge.State);
            AssertWorldUnloaded();
        }

        [UnityTest]
        public IEnumerator ConcurrentOpenRequestsAreRejectedWithoutReplacingTheAcceptedSession()
        {
            Send(Command("OpenWorld", "accepted-open"));
            Send(Command("OpenWorld", "busy-loading"));
            yield return WaitForEvent("WorldFailed", "busy-loading");
            yield return WaitForEvent("WorldReady", "accepted-open");
            Send(Command("OpenWorld", "busy-ready"));
            yield return WaitForEvent("WorldFailed", "busy-ready");

            Assert.AreEqual("BUSY", Event("WorldFailed", "busy-loading").code);
            Assert.AreEqual("BUSY", Event("WorldFailed", "busy-ready").code);
            Assert.AreEqual("accepted-open", bridge.CurrentOpenRequestId);
            Assert.AreEqual(1, Enumerable.Range(0, SceneManager.sceneCount)
                .Select(SceneManager.GetSceneAt).Count(scene => scene.name == HostProtocol.DemoSceneName));
            Assert.AreEqual(1, events.Count(item => item.type == "WorldReady"));
        }

        [UnityTest]
        public IEnumerator ConcurrentCloseRequestsShareCleanupAndEachReceiveTheirOwnAcknowledgement()
        {
            Send(Command("OpenWorld", "open-for-close"));
            yield return WaitForEvent("WorldReady", "open-for-close");
            Send(Command("CloseWorld", "close-first"));
            Send(Command("CloseWorld", "close-second"));
            yield return WaitForEvent("WorldClosed", "close-first");
            yield return WaitForEvent("WorldClosed", "close-second");

            Assert.AreEqual(1, events.Count(item => item.type == "WorldClosed" && item.requestId == "close-first"));
            Assert.AreEqual(1, events.Count(item => item.type == "WorldClosed" && item.requestId == "close-second"));
            Assert.IsFalse(events.Any(item => item.type == "WorldFailed"));
            Assert.AreEqual(WorldRuntimeState.Idle, bridge.State);
            AssertWorldUnloaded();
            Send(Command("CloseWorld", "close-already-idle"));
            yield return WaitForEvent("WorldClosed", "close-already-idle");
            Assert.AreEqual("ALREADY_CLOSED", Event("WorldClosed", "close-already-idle").code);
        }

        static HostCommand Command(string type, string requestId) => new HostCommand
        {
            protocolVersion = HostProtocol.Version,
            type = type,
            requestId = requestId,
            worldId = HostProtocol.DemoWorldId,
            revisionId = HostProtocol.DemoRevisionId
        };

        void Send(HostCommand command) => bridge.Receive(JsonUtility.ToJson(command));
        void Capture(HostEvent payload) => events.Add(payload);
        HostEvent Event(string type, string requestId) =>
            events.Single(item => item.type == type && item.requestId == requestId);

        IEnumerator WaitForEvent(string type, string requestId) =>
            WaitUntil(() => events.Any(item => item.type == type && item.requestId == requestId), type + " for " + requestId);

        static IEnumerator WaitUntil(Func<bool> condition, string description)
        {
            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(condition(), "Timed out waiting for " + description);
        }

        static void AssertWorldUnloaded()
        {
            Scene scene = SceneManager.GetSceneByName(HostProtocol.DemoSceneName);
            Assert.IsFalse(scene.IsValid() && scene.isLoaded);
        }
    }
}
