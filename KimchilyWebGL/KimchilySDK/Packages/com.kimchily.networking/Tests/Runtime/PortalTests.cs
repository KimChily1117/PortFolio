using System.Collections;
using Kimchily.Creator.Mobile;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Kimchily.Networking.Tests
{
    public sealed class PortalTests
    {
        static PortalGameState State(long version = 1) => new PortalGameState
        {
            preset = CoopPortalApi.Preset, phase = "waiting", round = 1, requiredPlayers = 2,
            holdSeconds = 3, remainingMs = 3000, version = version,
            pads = new[]
            {
                new PortalPadState { id = "star", x = -3, z = 2, radius = 1.1f, active = true },
                new PortalPadState { id = "moon", x = 3, z = 2, radius = 1.1f, active = true },
                new PortalPadState { id = "sun", x = -3, z = 6, radius = 1.1f },
                new PortalPadState { id = "leaf", x = 3, z = 6, radius = 1.1f }
            }
        };

        [Test] public void ReadsGameSnapshotAndRejectsUnboundedOrMalformedServerData()
        {
            var envelope = JsonUtility.FromJson<ChatEvent>("{\"protocolVersion\":1,\"type\":\"game\",\"game\":" + JsonUtility.ToJson(State()) + "}");
            Assert.IsTrue(CoopPortalClient.IsValidState(envelope.game));
            Assert.AreEqual("moon", envelope.game.pads[1].id);
            Assert.AreEqual(2, envelope.game.requiredPlayers);
            envelope.game.remainingMs = 3001; Assert.IsFalse(CoopPortalClient.IsValidState(envelope.game));
            envelope.game = State(); envelope.game.pads[2].id = "moon"; Assert.IsFalse(CoopPortalClient.IsValidState(envelope.game));
            envelope.game = State(); envelope.game.pads[0].radius = float.NaN; Assert.IsFalse(CoopPortalClient.IsValidState(envelope.game));
            envelope.game = State(); envelope.game.requiredPlayers = 8; Assert.IsFalse(CoopPortalClient.IsValidState(envelope.game));
        }

        [Test] public void RoundActionDoesNotOverlapMobileMovementOrJumpControls()
        {
            foreach (var screen in new[] { new Vector2(390, 844), new Vector2(844, 390), new Vector2(320, 568) })
            {
                var safeArea = new Rect(0, 24, screen.x, screen.y - 48);
                float scale = screen.y / 600;
                var local = PortalGameHud.CalculateActionRect(safeArea, scale);
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
                var status = PortalGameHud.CalculateStatusRect(new Vector2(width, 600));
                Assert.That(status.xMin, Is.GreaterThanOrEqualTo(0));
                Assert.That(status.xMax, Is.LessThan(width * .4f), "The player's central speech-bubble column must remain unobstructed.");
            }
            var desktop = PortalGameHud.CalculateStatusRect(new Vector2(1280f / 1.2f, 600));
            var pixelRect = new Rect(desktop.position * 1.2f, desktop.size * 1.2f);
            Assert.IsFalse(pixelRect.Overlaps(new Rect(520, 142, 237, 100)), "Observed 1280x720 speech bubble.");
            var portrait = PortalGameHud.CalculateStatusRect(new Vector2(390f / (844f / 600), 600));
            Assert.AreEqual(112, portrait.y);
        }

        [UnityTest] public IEnumerator OptInCanPrecedeWorldBindingAndNeverLeaksToNextWorld()
        {
            var chat = InGameChat.Ensure();
            try
            {
                Assert.IsFalse(chat.Portal.GameEnabled);
                Assert.IsNull(chat.GetComponent<PortalGameHud>());
                Assert.IsFalse(CoopPortalApi.EnableGame("unknown-preset"));
                Assert.IsTrue(CoopPortalApi.EnableGame(CoopPortalApi.Preset));
                chat.Portal.EnterWorld("chili-island", "r1");
                Assert.IsTrue(chat.Portal.GameEnabled);
                Assert.IsFalse(CoopPortalApi.StartRound(), "Joining alone must not send a start before the user requests it.");
                var hud = chat.GetComponent<PortalGameHud>();
                Assert.AreEqual(RenderMode.ScreenSpaceOverlay, hud.Canvas.renderMode);
                Assert.IsFalse(hud.ActionButton.gameObject.activeSelf);
                Assert.That(CoopPortalApi.GetStateJson(), Does.Contain("\"game\":null"));
                chat.Portal.EnterWorld("other-world", "r2");
                Assert.IsFalse(chat.Portal.GameEnabled);
                Assert.IsFalse(hud.Canvas.gameObject.activeSelf);
                yield return null;
            }
            finally { if (chat != null) Object.Destroy(chat.gameObject); }
        }

        [UnityTest] public IEnumerator OlderRoundPacketsCannotOverwriteLatestStateAndExitClearsIt()
        {
            var chat = InGameChat.Ensure();
            try
            {
                CoopPortalApi.EnableGame(CoopPortalApi.Preset);
                var newest = State(10); newest.phase = "complete"; newest.remainingMs = 0;
                Assert.IsTrue(chat.Portal.AcceptState(newest));
                Assert.IsFalse(chat.Portal.AcceptState(State(9)));
                Assert.IsFalse(chat.Portal.AcceptState(State(10)));
                Assert.AreEqual("complete", chat.Portal.State.phase);
                Assert.That(CoopPortalApi.GetStateJson(), Does.Contain("\"phase\":\"complete\""));
                chat.ExitWorld();
                Assert.IsNull(chat.Portal.State);
                Assert.IsFalse(chat.Portal.GameEnabled);
                Assert.That(CoopPortalApi.GetStateJson(), Does.Contain("\"game\":null"));
                yield return null;
            }
            finally { if (chat != null) Object.Destroy(chat.gameObject); }
        }
    }
}
