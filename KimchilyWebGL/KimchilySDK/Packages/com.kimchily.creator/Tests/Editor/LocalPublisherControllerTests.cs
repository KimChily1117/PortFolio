using System;
using System.IO;
using Kimchily.Creator.Editor;
using NUnit.Framework;

namespace Kimchily.Creator.Tests
{
    public sealed class LocalPublisherControllerTests
    {
        string root;
        string publisher;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "kimchily-local-server-test-" + Guid.NewGuid().ToString("N"));
            publisher = Path.Combine(root, "KimchilyPublish");
            Directory.CreateDirectory(Path.Combine(publisher, "tools"));
            Directory.CreateDirectory(Path.Combine(publisher, ".local"));
            File.WriteAllText(Path.Combine(publisher, "tools", "start.ps1"), "# fixture");
            File.WriteAllText(Path.Combine(publisher, "server.py"), "# fixture");
            File.WriteAllText(Path.Combine(publisher, ".local", "token"), "fixture-token");
        }

        [TearDown]
        public void TearDown() { if (Directory.Exists(root)) Directory.Delete(root, true); }

        LocalPublisherStatus Ready() => new LocalPublisherStatus
        {
            schemaVersion = 1, status = "running", processVerified = true, healthy = true,
            localUrl = "http://127.0.0.1:8787", stateDirectory = Path.Combine(publisher, ".local")
        };

        [Test]
        public void StoppedManagementResponseAllowsANullPid()
        {
            var status = UnityEngine.JsonUtility.FromJson<LocalPublisherStatus>("{\"schemaVersion\":1,\"status\":\"stopped\",\"pid\":null,\"processVerified\":false,\"healthy\":false}");
            Assert.AreEqual("stopped", status.status);
            Assert.IsFalse(status.IsReady);
        }

        [Test]
        public void FindsSiblingPublisherFromNestedUnityProject()
        {
            string project = Path.Combine(root, "Projects", "Creator");
            Directory.CreateDirectory(project);
            Assert.AreEqual(publisher, LocalPublisherController.FindPublisherDirectory(project));
        }

        [Test]
        public void HealthyOwnedLoopbackServerCanConnectItsLocalToken()
        {
            Assert.AreEqual("fixture-token", LocalPublisherController.ReadLocalToken(publisher, Ready()));
        }

        [TestCase("http://192.168.0.4:8787")]
        [TestCase("http://127.0.0.1:8787/other")]
        [TestCase("http://name:password@127.0.0.1:8787")]
        public void LocalTokenIsNeverAppliedToRemoteOrCredentialedOrigins(string origin)
        {
            var status = Ready();
            status.localUrl = origin;
            Assert.Throws<InvalidDataException>(() => LocalPublisherController.ReadLocalToken(publisher, status));
        }

        [Test]
        public void UnhealthyOrUnverifiedProcessCannotConnectCredentials()
        {
            var status = Ready();
            status.processVerified = false;
            Assert.Throws<InvalidOperationException>(() => LocalPublisherController.ReadLocalToken(publisher, status));
            status = Ready();
            status.healthy = false;
            Assert.Throws<InvalidOperationException>(() => LocalPublisherController.ReadLocalToken(publisher, status));
        }

        [Test]
        public void DifferentStateDirectoryCannotSupplyTheToken()
        {
            var status = Ready();
            status.stateDirectory = Path.Combine(root, "another-server");
            Assert.Throws<InvalidDataException>(() => LocalPublisherController.ReadLocalToken(publisher, status));
        }

        [TestCase("http://192.168.0.4:8787")]
        [TestCase("http://127.0.0.1:9000")]
        [TestCase("http://127.0.0.1:8787/other")]
        public void AnAutomaticallyConnectedTokenCannotBeSentToAnEditedServer(string destination)
        {
            Assert.Throws<InvalidOperationException>(() =>
                LocalPublisherController.ValidateCredentialDestination("http://127.0.0.1:8787", destination));
            Assert.DoesNotThrow(() => LocalPublisherController.ValidateCredentialDestination("http://127.0.0.1:8787", "http://127.0.0.1:8787/"));
        }
    }
}
