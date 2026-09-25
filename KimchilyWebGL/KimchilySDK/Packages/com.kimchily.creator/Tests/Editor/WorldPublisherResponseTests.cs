using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;

namespace Kimchily.Creator.Editor.Tests
{
    public sealed class WorldPublisherResponseTests
    {
        const string Origin = "http://192.168.0.4:8788";
        const string World = "sample-world";
        const string Revision = "webgl-test-revision";
        static readonly string Hash = new string('a', 64);

        static WorldPublishResponse Response(string platform)
        {
            string manifest = Origin + "/worlds/" + World + "/" + Revision + "/world.json";
            return new WorldPublishResponse
            {
                worldId = World, revisionId = Revision, manifestSha256 = Hash,
                manifestUrl = manifest,
                publishUrl = Origin + "/w/" + World + "/" + Revision,
                qrUrl = Origin + "/qr/" + World + "/" + Revision + ".png",
                launchUrl = (platform == "WebGL" ? Origin + "/player/" : "kimchily://world") +
                    "?manifest=" + Uri.EscapeDataString(manifest) + "&sha256=" + Hash
            };
        }

        [TestCase("Android")]
        [TestCase("WebGL")]
        public void ValidPublicationBindsLinksToUploadedManifest(string platform)
        {
            Assert.DoesNotThrow(() => WorldPublisherClient.ValidatePublicationResponse(Response(platform), World, Revision, Hash, platform));
        }

        [TestCase("other-player-origin")]
        [TestCase("other-manifest")]
        [TestCase("wrong-hash")]
        [TestCase("duplicate-parameter")]
        [TestCase("extra-parameter")]
        [TestCase("wrong-player-path")]
        [TestCase("fragment")]
        [TestCase("other-qr-origin")]
        [TestCase("wrong-revision")]
        [TestCase("android-link")]
        public void WebPublicationRejectsUnboundOrAmbiguousLinks(string change)
        {
            var response = Response("WebGL");
            switch (change)
            {
                case "other-player-origin": response.launchUrl = response.launchUrl.Replace(Origin, "https://other.example"); break;
                case "other-manifest": response.launchUrl = response.launchUrl.Replace(Uri.EscapeDataString(response.manifestUrl), Uri.EscapeDataString(Origin + "/worlds/other/r/world.json")); break;
                case "wrong-hash": response.launchUrl = response.launchUrl.Replace(Hash, new string('b', 64)); break;
                case "duplicate-parameter": response.launchUrl = Origin + "/player/?sha256=" + Hash + "&sha256=" + Hash; break;
                case "extra-parameter": response.launchUrl += "&redirect=https://other.example"; break;
                case "wrong-player-path": response.launchUrl = response.launchUrl.Replace("/player/", "/other/"); break;
                case "fragment": response.launchUrl += "#unexpected"; break;
                case "other-qr-origin": response.qrUrl = response.qrUrl.Replace(Origin, "https://other.example"); break;
                case "wrong-revision": response.manifestUrl = response.manifestUrl.Replace(Revision, "other-revision"); break;
                case "android-link": response.launchUrl = Response("Android").launchUrl; break;
            }
            Assert.Throws<InvalidDataException>(() => WorldPublisherClient.ValidatePublicationResponse(response, World, Revision, Hash, "WebGL"));
        }

        [Test]
        public void PublishTargetsIncludeWebAndAndroidOnly()
        {
            Assert.That(WorldContentBuilder.IsPublishTarget(BuildTarget.WebGL), Is.True);
            Assert.That(WorldContentBuilder.IsPublishTarget(BuildTarget.Android), Is.True);
            Assert.That(WorldContentBuilder.IsPublishTarget(BuildTarget.StandaloneWindows64), Is.False);
        }
    }
}
