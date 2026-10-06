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
        public void EndpointValidation(string value, bool valid)
        {
            Assert.AreEqual(valid, ChatValidation.IsEndpoint(value));
        }

        [TestCase("demo-world_1", true)]
        [TestCase("lobby", true)]
        [TestCase("", false)]
        [TestCase("room/path", false)]
        [TestCase("space here", false)]
        [TestCase("한글", false)]
        public void RoomIdentityValidation(string value, bool valid)
        {
            Assert.AreEqual(valid, ChatValidation.IsId(value));
        }

        [Test]
        public void ChatSupportsKoreanButRejectsBlankControlsAndOversize()
        {
            Assert.IsTrue(ChatValidation.IsText("안녕하세요 <script>문자</script>", 300));
            Assert.IsFalse(ChatValidation.IsText("  ", 300));
            Assert.IsFalse(ChatValidation.IsText("line\nbreak", 300));
            Assert.IsFalse(ChatValidation.IsText(new string('x', 301), 300));
        }

        [Test]
        public void ReadsServerPlayerIdentityAndIsoTimestamp()
        {
            var message = JsonUtility.FromJson<ChatEvent>("{\"protocolVersion\":1,\"type\":\"joined\",\"players\":[{\"playerId\":\"server-id\",\"name\":\"칠리\"}],\"history\":[{\"id\":\"m1\",\"playerId\":\"server-id\",\"name\":\"칠리\",\"text\":\"hello\",\"sentAtUtc\":\"2026-09-29T09:00:00+00:00\"}]}");
            Assert.AreEqual("server-id", message.players[0].playerId);
            Assert.AreEqual("2026-09-29T09:00:00+00:00", message.history[0].sentAtUtc);
        }

        [UnityTest]
        public IEnumerator ExpandedChatPausesAndRestoresOnlyEnabledControls()
        {
            var enabledRoot = new GameObject("enabled controls");
            var disabledRoot = new GameObject("disabled controls");
            var controls = enabledRoot.AddComponent<KimchilyMobileControls>();
            var disabled = disabledRoot.AddComponent<KimchilyMobileControls>();
            disabled.enabled = false;
            var chat = InGameChat.Ensure();
            try
            {
                chat.SetExpanded("true");
                Assert.IsFalse(controls.enabled);
                Assert.IsFalse(disabled.enabled);
                chat.SetContext("demo", "r1");
                Assert.IsFalse(controls.enabled);
                Assert.IsFalse(chat.View.joined);
                chat.SetExpanded("false");
                Assert.IsTrue(controls.enabled);
                Assert.IsFalse(disabled.enabled);
                chat.SetExpanded("true");
                Object.Destroy(chat.gameObject);
                yield return null;
                Assert.IsTrue(controls.enabled);
                Assert.IsFalse(disabled.enabled);
            }
            finally
            {
                Object.Destroy(enabledRoot);
                Object.Destroy(disabledRoot);
                if (chat != null)
                {
                    Object.Destroy(chat.gameObject);
                }
            }
        }

        [UnityTest]
        public IEnumerator SwitchingWorldClearsRoomDataAndDoesNotConnectWithoutUserAction()
        {
            var chat = InGameChat.Ensure();
            try
            {
                chat.SetContext("demo", "r1");
                chat.SetContext("other", "r2");
                chat.OnWire("{\"generation\":-1,\"type\":\"message\",\"data\":\"{\\\"protocolVersion\\\":1,\\\"type\\\":\\\"joined\\\"}\"}");
                yield return null;
                Assert.AreEqual("other", chat.View.worldId);
                Assert.AreEqual("r2", chat.View.revisionId);
                Assert.IsFalse(chat.View.joined);
                Assert.IsFalse(chat.View.connecting);
                Assert.IsEmpty(chat.View.messages);
                Assert.IsEmpty(chat.View.players);
            }
            finally
            {
                if (chat != null)
                {
                    Object.Destroy(chat.gameObject);
                }
            }
        }

        [UnityTest]
        public IEnumerator ChatUsesCanvasAndTmpAndSpeechDoesNotInterpretMarkup()
        {
            var chat = InGameChat.Ensure();
            var root = new GameObject("speech owner");
            try
            {
                var panel = chat.GetComponent<UnityChatPanel>();
                Assert.AreEqual(RenderMode.ScreenSpaceOverlay, panel.Canvas.renderMode);
                Assert.IsNotNull(panel.MessageInput);
                Assert.IsNotNull(UnityChatPanel.Font);
                Assert.IsTrue(UnityChatPanel.Font.HasCharacter('한', true, true), "The shipped font must render Korean at runtime.");
                Assert.IsFalse(panel.MessageInput.richText);
                var speech = PlayerSpeech.Create(root.transform, "칠리", () => null);
                speech.Say("<b>안녕하세요</b>");
                Assert.AreEqual("<b>안녕하세요</b>", speech.CurrentText);
                foreach (var text in speech.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true))
                {
                    Assert.IsFalse(text.richText);
                }

                speech.HideSpeech();
                Assert.AreEqual("", speech.CurrentText);
                yield return null;
            }
            finally
            {
                Object.Destroy(root);
                if (chat != null)
                {
                    Object.Destroy(chat.gameObject);
                }
            }
        }

        [UnityTest]
        public IEnumerator RemoteAvatarHasNoLocalControllerAndIgnoresOlderPoses()
        {
            var player = KimchilyMobilePlayerBootstrap.CreateForScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            var root = new GameObject("remote test");
            root.SetActive(false);
            try
            {
                var remote = root.AddComponent<RemoteAvatar>();
                remote.Initialize(player, "친구");
                root.SetActive(true);
                remote.Apply(new ChatPose { sequence = 2, x = 2, y = 3, z = 4, yaw = 90, grounded = true });
                remote.Apply(new ChatPose { sequence = 1, x = 100 });
                yield return null;
                Assert.AreEqual(2, remote.LastSequence);
                Assert.That(Vector3.Distance(root.transform.position, new Vector3(2, 3, 4)), Is.LessThan(.01f));
                Assert.IsNull(root.GetComponentInChildren<KimchilyMobilePlayer>());
                Assert.IsNull(root.GetComponentInChildren<KimchilyMobileControls>());
                Assert.IsNull(root.GetComponentInChildren<Camera>());
            }
            finally
            {
                Object.Destroy(root);
                Object.Destroy(player.gameObject);
            }
        }
    }
}
