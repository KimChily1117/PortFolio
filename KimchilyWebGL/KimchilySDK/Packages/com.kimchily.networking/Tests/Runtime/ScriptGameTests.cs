using System;
using System.Collections;
using Kimchily.Creator.Mobile;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Kimchily.Networking.Tests
{
    public sealed class ScriptGameTests
    {
        const string ScriptId = "test-game";
        static readonly string Hash = new string('a', 64);
        static ScriptGameState State(long version = 1) => new ScriptGameState
        {
            scriptId = ScriptId, scriptHash = Hash, version = version, stateJson = "{\"score\":8,\"custom\":true}"
        };
        static string HudJson(string id = "continue", bool enabled = true) => JsonUtility.ToJson(new WorldHudModel
        {
            eyebrow = "SDK DEMO", title = "<b>한글 제목</b>", body = "첫 번째 안내\n두 번째 안내",
            progress = .5f, accent = "#AABBCC", action = new WorldHudAction { id = id, label = "계속하기", enabled = enabled }
        });

        [Test] public void EnvelopeValidatesIdentityAndByteBoundsWithoutKnowingGameSchema()
        {
            var envelope = JsonUtility.FromJson<ChatEvent>("{\"protocolVersion\":1,\"type\":\"game\",\"game\":" + JsonUtility.ToJson(State()) + "}");
            Assert.IsTrue(ScriptGameClient.IsValidState(envelope.game));
            Assert.AreEqual("{\"score\":8,\"custom\":true}", envelope.game.stateJson);
            envelope.game.scriptHash = Hash.ToUpperInvariant(); Assert.IsFalse(ScriptGameClient.IsValidState(envelope.game));
            envelope.game = State(); envelope.game.scriptId = "path/escape"; Assert.IsFalse(ScriptGameClient.IsValidState(envelope.game));
            envelope.game = State(); envelope.game.version = -1; Assert.IsFalse(ScriptGameClient.IsValidState(envelope.game));
            envelope.game = State(); envelope.game.stateJson = new string('한', 6000);
            Assert.IsFalse(ScriptGameClient.IsValidState(envelope.game), "UTF-8 bytes, not only character count, bound the opaque payload.");
            Assert.IsTrue(ScriptGameClient.IsValidIdentity(new string('g', 80), Hash));
            Assert.IsFalse(ScriptGameClient.IsValidIdentity(new string('g', 81), Hash));
        }

        [UnityTest] public IEnumerator OptInBeforeWorldBindingHasNoHudAndDoesNotLeakToNextWorld()
        {
            var chat = InGameChat.Ensure();
            try
            {
                Assert.IsFalse(chat.Game.GameEnabled);
                Assert.IsFalse(ScriptRoomApi.UseGame(ScriptId, "invalid-hash"));
                Assert.IsTrue(ScriptRoomApi.UseGame(ScriptId, Hash));
                Assert.IsFalse(ScriptRoomApi.UseGame("another-game", Hash));
                chat.Game.EnterWorld("first-world", "r1");
                Assert.IsTrue(chat.Game.GameEnabled);
                Assert.IsFalse(ScriptRoomApi.SendAction("arbitrary-action", "{}"), "An unjoined client must not send.");
                Assert.IsNull(chat.GetComponent<WorldHudPanel>(), "Network subscription must not create presentation.");
                Assert.That(ScriptRoomApi.GetStateJson(), Does.Contain("\"game\":null"));
                chat.Game.EnterWorld("other-world", "r2");
                Assert.IsFalse(chat.Game.GameEnabled);
                Assert.AreEqual("", chat.Game.ScriptId);
                yield return null;
            }
            finally { if (chat != null) Object.Destroy(chat.gameObject); }
        }

        [UnityTest] public IEnumerator IdentityAndVersionGateLateJoinWhileReconnectAcceptsFreshSnapshot()
        {
            var chat = InGameChat.Ensure();
            try
            {
                ScriptRoomApi.UseGame(ScriptId, Hash);
                chat.View.joined = true;
                chat.SetExpanded("false"); // 연결 상태 변경 알림을 통해 실제 수명 경로를 사용한다.
                Assert.IsTrue(chat.Game.AcceptState(State(10)));
                Assert.IsFalse(chat.Game.AcceptState(State(9)));
                Assert.IsFalse(chat.Game.AcceptState(State(10)));
                var foreign = State(11); foreign.scriptHash = new string('b', 64);
                Assert.IsFalse(chat.Game.AcceptState(foreign));
                foreign = State(11); foreign.scriptId = "other-game";
                Assert.IsFalse(chat.Game.AcceptState(foreign));
                Assert.AreEqual(10, chat.Game.State.version);
                chat.Disconnect("");
                Assert.IsTrue(chat.Game.GameEnabled);
                Assert.IsNull(chat.Game.State);
                chat.Game.OnServerEvent(new ChatEvent { type = "joined", game = State(1) });
                Assert.AreEqual(1, chat.Game.State.version, "A new connection uses the late-join snapshot, not the old connection's version.");
                Assert.That(ScriptRoomApi.GetStateJson(), Does.Contain("\"stateJson\":"));
                chat.ExitWorld();
                Assert.IsNull(chat.Game.State); Assert.IsFalse(chat.Game.GameEnabled);
                yield return null;
            }
            finally { if (chat != null) Object.Destroy(chat.gameObject); }
        }

        [UnityTest] public IEnumerator ActionsAreGenericButBoundedAndRateLimited()
        {
            var chat = InGameChat.Ensure();
            try
            {
                ScriptRoomApi.UseGame(ScriptId, Hash);
                chat.View.joined = true;
                Assert.IsFalse(ScriptRoomApi.SendAction("not a valid id", "{}"));
                Assert.IsFalse(ScriptRoomApi.SendAction("custom", "\"" + new string('한', 342) + "\""));
                Assert.IsTrue(ScriptRoomApi.SendAction("custom", "{\"value\":12}"));
                Assert.IsFalse(ScriptRoomApi.SendAction("another", "null"), "All game actions share the generic burst guard.");
                yield return null;
            }
            finally { if (chat != null) Object.Destroy(chat.gameObject); }
        }

        [UnityTest] public IEnumerator HudRejectsInvalidInputBeforeAllocationAndRendersOnlyPlainText()
        {
            var owner = new GameObject("HUD validation");
            var hud = owner.AddComponent<WorldHudPanel>();
            try
            {
                Assert.IsNull(hud.TakeAction()); Assert.IsNull(hud.Canvas);
                Assert.Throws<ArgumentException>(() => hud.ShowPanelJson("{\"title\":\"\",\"progress\":0}"));
                Assert.Throws<ArgumentException>(() => hud.ShowPanelJson(new string('x', 4097)));
                Assert.IsNull(hud.Canvas);
                hud.ShowPanelJson(HudJson());
                Assert.AreEqual(RenderMode.ScreenSpaceOverlay, hud.Canvas.renderMode);
                Assert.IsTrue(hud.IsVisible);
                foreach (var text in hud.Canvas.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true)) Assert.IsFalse(text.richText);
                Assert.Throws<ArgumentException>(() => hud.ShowPanelJson("{\"title\":\"Valid\",\"progress\":2}"));
                Assert.IsTrue(hud.IsVisible, "Rejected replacements must preserve the valid displayed model.");
                yield return null;
            }
            finally { Object.Destroy(owner); }
        }

        [UnityTest] public IEnumerator HudActionsAreOwnerScopedSingleUseAndClearedOnReplacementHideOrDisable()
        {
            var firstOwner = new GameObject("first HUD");
            var secondOwner = new GameObject("second HUD");
            var first = firstOwner.AddComponent<WorldHudPanel>(); var second = secondOwner.AddComponent<WorldHudPanel>();
            try
            {
                first.ShowPanelJson(HudJson()); second.ShowPanelJson(HudJson("other"));
                first.ActionButton.onClick.Invoke(); first.ActionButton.onClick.Invoke();
                Assert.IsNull(second.TakeAction()); Assert.AreEqual("continue", first.TakeAction()); Assert.IsNull(first.TakeAction());
                first.ActionButton.onClick.Invoke(); first.ShowPanelJson(HudJson("replacement"));
                Assert.IsNull(first.TakeAction());
                first.ActionButton.onClick.Invoke(); first.Hide();
                Assert.IsNull(first.TakeAction()); Assert.IsFalse(first.Canvas.gameObject.activeSelf);
                first.ShowPanelJson(HudJson(enabled: false)); first.ActionButton.onClick.Invoke();
                Assert.IsNull(first.TakeAction());
                first.ShowPanelJson(HudJson()); first.ActionButton.onClick.Invoke(); first.enabled = false;
                Assert.IsNull(first.TakeAction()); Assert.IsFalse(first.IsVisible);
                yield return null;
            }
            finally { Object.Destroy(firstOwner); Object.Destroy(secondOwner); }
        }

        [UnityTest] public IEnumerator HudAllowsMissingOrNullActionButRejectsMalformedPresentAction()
        {
            var owner = new GameObject("optional HUD action");
            var hud = owner.AddComponent<WorldHudPanel>();
            try
            {
                foreach (string json in new[]
                {
                    "{\"title\":\"진행 중\"}",
                    "{\"title\":\"진행 중\",\"action\":null}",
                    "{\"title\":\"진행 중\",\"\\u0061ction\":null}"
                })
                {
                    Assert.DoesNotThrow(() => hud.ShowPanelJson(json));
                    Assert.IsTrue(hud.IsVisible); Assert.IsFalse(hud.ActionButton.gameObject.activeSelf);
                }
                Assert.Throws<ArgumentException>(() => hud.ShowPanelJson("{\"title\":\"진행 중\",\"action\":{}}"));
                Assert.Throws<ArgumentException>(() => hud.ShowPanelJson("{\"title\":\"진행 중\",\"action\":{\"action\":null}}"));
                Assert.Throws<ArgumentException>(() => hud.ShowPanelJson("{\"title\":\"진행 중\",\"action\":null,\"action\":{}}"));
                hud.ShowPanelJson("{\"title\":\"진행 중\",\"body\":\"\\\"action\\\":null\",\"action\":{\"id\":\"continue\",\"label\":\"계속\",\"enabled\":true}}");
                Assert.IsTrue(hud.ActionButton.gameObject.activeSelf);
                hud.ActionButton.onClick.Invoke(); Assert.AreEqual("continue", hud.TakeAction());
                yield return null;
            }
            finally { Object.Destroy(owner); }
        }

        [Test] public void HudActionDoesNotOverlapMobileMovementOrJumpControls()
        {
            foreach (var screen in new[] { new Vector2(390, 844), new Vector2(844, 390), new Vector2(320, 568) })
            {
                var safeArea = new Rect(0, 24, screen.x, screen.y - 48);
                float scale = screen.y / 600;
                var local = WorldHudPanel.CalculateActionRect(safeArea, scale);
                var button = new Rect(safeArea.position + local.position * scale, local.size * scale);
                var controls = MobileControlLayout.Calculate(safeArea);
                Assert.IsFalse(button.Overlaps(controls.Joystick), screen + " joystick");
                Assert.IsFalse(button.Overlaps(controls.Jump), screen + " jump");
                Assert.That(button.xMin, Is.GreaterThanOrEqualTo(safeArea.xMin));
                Assert.That(button.xMax, Is.LessThanOrEqualTo(safeArea.xMax));
            }
        }

        [Test] public void DesktopStatusLeavesCenterSpeechBubbleVisible()
        {
            foreach (float width in new[] { 650f, 800f, 1280f / 1.2f })
            {
                var status = WorldHudPanel.CalculateStatusRect(new Vector2(width, 600));
                Assert.That(status.xMin, Is.GreaterThanOrEqualTo(0));
                Assert.That(status.xMax, Is.LessThan(width * .4f));
            }
            var desktop = WorldHudPanel.CalculateStatusRect(new Vector2(1280f / 1.2f, 600));
            var pixelRect = new Rect(desktop.position * 1.2f, desktop.size * 1.2f);
            Assert.IsFalse(pixelRect.Overlaps(new Rect(520, 142, 237, 100)));
            Assert.AreEqual(112, WorldHudPanel.CalculateStatusRect(new Vector2(390f / (844f / 600), 600)).y);
        }
    }
}
