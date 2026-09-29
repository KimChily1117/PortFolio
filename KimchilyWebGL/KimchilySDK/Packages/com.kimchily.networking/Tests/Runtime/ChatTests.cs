using System.Collections;
using Kimchily.Creator.Mobile;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Kimchily.Networking.Tests
{
    public sealed class ChatTests
    {
        [TestCase("ws://127.0.0.1:8790/ws", true)]
        [TestCase("wss://chat.example/ws", true)]
        [TestCase("https://chat.example/ws", false)]
        [TestCase("ws://user:secret@chat.example/ws", false)]
        [TestCase("ws://chat.example/ws?token=secret", false)]
        [TestCase("ws://chat.example/other", false)]
        [TestCase("ws://chat.example/ws#x", false)]
        public void EndpointValidation(string value, bool valid) => Assert.AreEqual(valid, ChatValidation.IsEndpoint(value));

        [TestCase("demo-world_1", true)]
        [TestCase("lobby", true)]
        [TestCase("", false)]
        [TestCase("room/path", false)]
        [TestCase("space here", false)]
        [TestCase("한글", false)]
        public void RoomIdentityValidation(string value, bool valid) => Assert.AreEqual(valid, ChatValidation.IsId(value));

        [Test] public void ChatSupportsKoreanButRejectsBlankControlsAndOversize()
        {
            Assert.IsTrue(ChatValidation.IsText("안녕하세요 <script>문자</script>", 300));
            Assert.IsFalse(ChatValidation.IsText("  ", 300));
            Assert.IsFalse(ChatValidation.IsText("line\nbreak", 300));
            Assert.IsFalse(ChatValidation.IsText(new string('x', 301), 300));
        }
        [Test] public void ReadsServerPlayerIdentityAndIsoTimestamp()
        {
            var message = JsonUtility.FromJson<ChatEvent>("{\"protocolVersion\":1,\"type\":\"joined\",\"players\":[{\"playerId\":\"server-id\",\"name\":\"칠리\"}],\"history\":[{\"id\":\"m1\",\"playerId\":\"server-id\",\"name\":\"칠리\",\"text\":\"hello\",\"sentAtUtc\":\"2026-09-29T09:00:00+00:00\"}]}");
            Assert.AreEqual("server-id", message.players[0].playerId);
            Assert.AreEqual("2026-09-29T09:00:00+00:00", message.history[0].sentAtUtc);
        }
        [UnityTest] public IEnumerator ExpandedChatPausesAndRestoresOnlyEnabledControls()
        {
            var enabledRoot = new GameObject("enabled controls");
            var disabledRoot = new GameObject("disabled controls");
            var controls = enabledRoot.AddComponent<KimchilyMobileControls>();
            var disabled = disabledRoot.AddComponent<KimchilyMobileControls>(); disabled.enabled = false;
            var chat = InGameChat.Ensure();
            try
            {
                chat.SetExpanded("true");
                Assert.IsFalse(controls.enabled); Assert.IsFalse(disabled.enabled);
                chat.SetContext("demo", "r1");
                Assert.IsFalse(controls.enabled); Assert.IsFalse(chat.View.joined);
                chat.SetExpanded("false");
                Assert.IsTrue(controls.enabled); Assert.IsFalse(disabled.enabled);
                chat.SetExpanded("true");
                Object.Destroy(chat.gameObject);
                yield return null;
                Assert.IsTrue(controls.enabled); Assert.IsFalse(disabled.enabled);
            }
            finally
            {
                Object.Destroy(enabledRoot); Object.Destroy(disabledRoot);
                if (chat != null) Object.Destroy(chat.gameObject);
            }
        }
        [UnityTest] public IEnumerator SwitchingWorldClearsRoomDataAndDoesNotConnectWithoutUserAction()
        {
            var chat = InGameChat.Ensure();
            try
            {
                chat.SetContext("demo", "r1");
                chat.SetContext("other", "r2");
                chat.OnWire("{\"generation\":-1,\"type\":\"message\",\"data\":\"{\\\"protocolVersion\\\":1,\\\"type\\\":\\\"joined\\\"}\"}");
                yield return null;
                Assert.AreEqual("other", chat.View.worldId); Assert.AreEqual("r2", chat.View.revisionId);
                Assert.IsFalse(chat.View.joined); Assert.IsFalse(chat.View.connecting);
                Assert.IsEmpty(chat.View.messages); Assert.IsEmpty(chat.View.players);
            }
            finally { if (chat != null) Object.Destroy(chat.gameObject); }
        }
    }
}
