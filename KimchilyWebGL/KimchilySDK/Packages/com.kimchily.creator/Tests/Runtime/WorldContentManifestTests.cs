using System;
using System.IO;
using NUnit.Framework;
using Kimchily.Creator.Content;
using UnityEngine;

namespace Kimchily.Creator.Tests
{
    public sealed class WorldContentManifestTests
    {
        WorldContentManifest Valid() => new WorldContentManifest
        {
            worldId = "world", revisionId = "revision", unityVersion = Application.unityVersion,
            platform = WorldContentSession.CurrentPlatform, renderPipeline = WorldContentSession.CurrentRenderPipeline,
            entryScene = "Assets/World.unity", scenes = new[] { "Assets/World.unity" },
            bundles = new[] { new BundleFile { name = "scene", fileName = "scene", sha256 = new string('a', 64) } }
        };

        [Test] public void AcceptsCompatibleManifest() =>
            Assert.DoesNotThrow(() => WorldContentSession.ValidateManifest(Valid()));
        [Test] public void RejectsDifferentSdkVersion()
        {
            var data = Valid(); data.sdkVersion = "999";
            Assert.Throws<InvalidDataException>(() => WorldContentSession.ValidateManifest(data));
        }
        [Test] public void RejectsUndeclaredEntryScene()
        {
            var data = Valid(); data.entryScene = "Assets/Other.unity";
            Assert.Throws<InvalidDataException>(() => WorldContentSession.ValidateManifest(data));
        }
        [Test] public void RejectsMissingPlayerComponent()
        {
            var data = Valid(); data.requiredTypes = new[] { new ScriptRequirement { assembly = "MissingSDK", type = "Missing.Behaviour" } };
            Assert.Throws<InvalidDataException>(() => WorldContentSession.ValidateManifest(data));
        }
        [Test] public void RejectsDifferentRenderPipeline()
        {
            var data = Valid(); data.renderPipeline = "unknown-pipeline";
            Assert.Throws<InvalidDataException>(() => WorldContentSession.ValidateManifest(data));
        }
        [Test] public void RejectsDuplicateBundleFiles()
        {
            var data = Valid(); data.bundles = new[] { data.bundles[0], new BundleFile { name = "other", fileName = "scene", sha256 = new string('a',64) } };
            Assert.Throws<InvalidDataException>(() => WorldContentSession.ValidateManifest(data));
        }
    }
}

