using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Kimchily.Creator;
using Kimchily.Creator.Content;

static class Program
{
    static int Main()
    {
        var tests = new (string, Action)[]
        {
            ("compatible content", () => Validate(Valid())),
            ("schema mismatch", () => Reject(m => m.schemaVersion = 99)),
            ("SDK mismatch", () => Reject(m => m.sdkVersion = "99")),
            ("Unity mismatch", () => Reject(m => m.unityVersion = "other")),
            ("platform mismatch", () => Reject(m => m.platform = "Android")),
            ("pipeline mismatch", () => Reject(m => m.renderPipeline = "other")),
            ("missing entry scene", () => Reject(m => m.entryScene = "Assets/Unknown.unity")),
            ("duplicate scene", () => Reject(m => m.scenes = new[] { m.entryScene, m.entryScene })),
            ("duplicate bundle filename", () => Reject(m => m.bundles[1].fileName = m.bundles[0].fileName)),
            ("path traversal", () => Reject(m => m.bundles[0].fileName = "../escape")),
            ("absolute Windows path", () => Reject(m => m.bundles[0].fileName = "C:\\escape")),
            ("invalid hash", () => Reject(m => m.bundles[0].sha256 = new string('z',64))),
            ("missing dependency", () => Reject(m => m.bundles[0].dependencies = new[] { "missing" })),
            ("dependency cycle", () => Reject(m => m.bundles[1].dependencies = new[] { "world-scenes" })),
            ("required C# absent", () => Reject(m => m.requiredTypes = new[] { new ScriptRequirement { assembly = "Absent", type = "Unknown" } })),
            ("explicit cleanup exactly once", CleanupOnce),
            ("cleanup survives inner disposal failure", CleanupAfterFailure)
        };
        int failed = 0;
        foreach (var test in tests)
        {
            try { test.Item2(); Console.WriteLine("PASS " + test.Item1); }
            catch (Exception ex) { failed++; Console.Error.WriteLine("FAIL " + test.Item1 + ": " + ex); }
        }
        Console.WriteLine((tests.Length - failed) + "/" + tests.Length + " passed (managed-only; no Unity execution).");
        return failed == 0 ? 0 : 1;
    }

    static WorldContentManifest Valid() => new WorldContentManifest
    {
        worldId = "test", revisionId = "r1", unityVersion = "2022.3.16f1",
        platform = "StandaloneWindows64", renderPipeline = "builtin",
        entryScene = "Assets/World.unity", scenes = new[] { "Assets/World.unity" },
        bundles = new[]
        {
            new BundleFile { name = "world-scenes", fileName = "world-scenes", sha256 = new string('a',64), dependencies = new[] { "world-assets" } },
            new BundleFile { name = "world-assets", fileName = "world-assets", sha256 = new string('b',64) }
        }
    };
    static void Validate(WorldContentManifest manifest) =>
        WorldManifestValidation.Validate(manifest, "2022.3.16f1", "StandaloneWindows64", "builtin", _ => false);
    static void Reject(Action<WorldContentManifest> mutate)
    {
        var data = Valid(); mutate(data);
        try { Validate(data); }
        catch (InvalidDataException) { return; }
        throw new Exception("Invalid content was accepted.");
    }
    static void CleanupOnce()
    {
        int count = 0;
        var iterator = CoroutineRoutine.WithCleanup(Wait(), () => count++);
        iterator.MoveNext();
        ((IDisposable)iterator).Dispose();
        ((IDisposable)iterator).Dispose();
        if (count != 1 || iterator.MoveNext()) throw new Exception("Cleanup/disposal contract failed.");
    }
    static void CleanupAfterFailure()
    {
        int count = 0;
        var iterator = CoroutineRoutine.WithCleanup(new BadDispose(), () => count++);
        try { ((IDisposable)iterator).Dispose(); throw new Exception("Expected inner disposal error."); }
        catch (InvalidOperationException) { }
        if (count != 1) throw new Exception("Explicit cleanup did not execute.");
    }
    static IEnumerator Wait() { while (true) yield return null; }
    sealed class BadDispose : IEnumerator, IDisposable
    {
        public object Current => null;
        public bool MoveNext() => true;
        public void Reset() { }
        public void Dispose() => throw new InvalidOperationException("expected disposal failure");
    }
}

