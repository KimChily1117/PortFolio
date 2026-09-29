using System;
using System.IO;
using Kimchily.TypeScript.Editor;
using NUnit.Framework;
using UnityEngine;

namespace Kimchily.TypeScript.Tests
{
    public sealed class TypeScriptCompilerSetupTests
    {
        string directory;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "KimchilyCompilerSetupTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        string FileAt(string relative)
        {
            string path = Path.GetFullPath(Path.Combine(directory, relative));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "test executable placeholder");
            return path;
        }

        void Ready(string relative, int schemaVersion = 1)
        {
            string marker = FileAt(".tools/ready.json");
            File.WriteAllText(marker, JsonUtility.ToJson(new TypeScriptCompiler.ReadyMarker
            {
                schemaVersion = schemaVersion,
                nodeRelativePath = relative,
                nodeVersion = "v24.21.0",
                typescriptVersion = "5.9.3"
            }));
        }

        [Test]
        public void ReadyMarkerSelectsPackageRuntimeBeforeMachineRuntimeAndHonorsOverride()
        {
            string bundled = FileAt(".tools/node-v24.21.0-win-x64/node.exe");
            string installed = FileAt("program files/nodejs/node.exe");
            Ready("node-v24.21.0-win-x64/node.exe");
            Assert.AreEqual(bundled, TypeScriptCompiler.ResolveNode(directory, null, Path.Combine(directory, "program files"), ""));
            Assert.AreEqual(installed, TypeScriptCompiler.ResolveNode(directory, installed, Path.Combine(directory, "program files"), ""));
            // An explicit stale override stays explicit; silently using a different
            // runtime would hide a configuration problem.
            Assert.AreEqual("missing-override", TypeScriptCompiler.ResolveNode(directory, "missing-override", "", ""));
        }

        [Test]
        public void IncompleteCorruptOrEscapingReadyMarkersDoNotSelectARuntime()
        {
            FileAt(".tools/node-v24.21.0-win-x64/node.exe");
            Assert.IsNull(TypeScriptCompiler.ReadBundledNode(directory), "An executable without a ready marker is still staging.");
            Ready("node-v24.21.0-win-x64/node.exe", 2);
            Assert.IsNull(TypeScriptCompiler.ReadBundledNode(directory));
            Ready("missing/node.exe");
            Assert.IsNull(TypeScriptCompiler.ReadBundledNode(directory));
            string escaped = FileAt("outside.exe");
            Ready("../outside.exe");
            Assert.IsNull(TypeScriptCompiler.ReadBundledNode(directory));
            Ready(escaped);
            Assert.IsNull(TypeScriptCompiler.ReadBundledNode(directory));
            File.WriteAllText(Path.Combine(directory, ".tools/ready.json"), "not JSON");
            Assert.IsNull(TypeScriptCompiler.ReadBundledNode(directory));
        }

        [Test]
        public void ExistingNodeInstallStillWorksWithoutPackageDownload()
        {
            string installed = FileAt("program files/nodejs/node.exe");
            Assert.AreEqual(installed, TypeScriptCompiler.ResolveNode(directory, null, Path.Combine(directory, "program files"), ""));
            string onPath = FileAt("bin/node");
            Assert.AreEqual(onPath, TypeScriptCompiler.ResolveNode(directory, null, "", Path.Combine(directory, "bin")));
        }

        [Test]
        public void CompilerLocksAllowParallelImportsButExcludeInstallerInBothDirections()
        {
            string lockPath = Path.Combine(directory, ".tools/install.lock");
            using (TypeScriptCompiler.AcquireCompilerLock(directory))
            using (TypeScriptCompiler.AcquireCompilerLock(directory))
                Assert.Throws<IOException>(() =>
                {
                    using (new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)) { }
                });
            using (new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                var error = Assert.Throws<InvalidOperationException>(() =>
                {
                    using (TypeScriptCompiler.AcquireCompilerLock(directory)) { }
                });
                StringAssert.Contains("setup is running", error.Message);
            }
            using (TypeScriptCompiler.AcquireCompilerLock(directory)) { }
        }

        [Test]
        public void MissingCompilerProducesActionableFailureWithoutCreatingAnyTools()
        {
            Assert.IsFalse(TypeScriptCompiler.TryGetToolchain(directory, out _, out string reason));
            StringAssert.Contains("not installed", reason);
            Assert.IsEmpty(Directory.GetFileSystemEntries(directory));
        }

        [TestCase("v12.22.0", false)]
        [TestCase("v17.9.1", false)]
        [TestCase("v18.0.0", true)]
        [TestCase("v24.21.0\r\n", true)]
        [TestCase("not node", false)]
        public void RuntimeVersionCheckRejectsOldOrUnrecognizedExecutables(string version, bool supported)
        {
            Assert.AreEqual(supported, TypeScriptCompiler.IsSupportedNodeVersion(version));
        }
    }
}
