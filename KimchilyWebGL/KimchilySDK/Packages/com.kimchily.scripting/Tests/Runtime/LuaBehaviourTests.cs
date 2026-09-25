using System.Collections;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Kimchily.Creator;
using UnityEngine;
using UnityEngine.TestTools;

namespace Kimchily.Scripting.Tests
{
    public sealed class LuaBehaviourTests
    {
        private GameObject owner;
        private GameObject target;
        private TextAsset asset;

        [TearDown]
        public void TearDown()
        {
            if (owner != null) Object.DestroyImmediate(owner);
            if (target != null) Object.DestroyImmediate(target);
            if (asset != null) Object.DestroyImmediate(asset);
        }

        private KimchilyLuaBehaviour Create(string source, int budget = 20000)
        {
            target = new GameObject("Lua reference");
            owner = new GameObject("Lua test");
            owner.SetActive(false);
            var behaviour = owner.AddComponent<KimchilyLuaBehaviour>();
            asset = new TextAsset(source) { name = "test.lua" };
            behaviour.ScriptAsset = asset;
            behaviour.References = new[] { new LuaObjectReference("target", target) };
            behaviour.InstructionBudget = budget;
            owner.SetActive(true);
            return behaviour;
        }

        [UnityTest]
        public IEnumerator LifecycleAndNamedReferenceManipulateOnlyAssignedObjects()
        {
            var behaviour = Create(@"
function on_enable() self.set_position(1, 2, 3) end
function on_start() refs.target.set_position(4, 5, 6) end
function on_update(dt) self.translate(dt, 0, 0) end");
            Assert.That(owner.transform.position, Is.EqualTo(new Vector3(1, 2, 3)));
            yield return null;
            yield return null;
            Assert.That(behaviour.IsReady, Is.True, behaviour.LastError);
            Assert.That(target.transform.position, Is.EqualTo(new Vector3(4, 5, 6)));
            Assert.That(owner.transform.position.x, Is.GreaterThan(1));
        }

        [UnityTest]
        public IEnumerator CoroutineResumesAcrossFramesAndWaitSeconds()
        {
            var behaviour = Create(@"
function on_start()
 start(function()
  self.set_position(1, 0, 0)
  next_frame()
  self.set_position(2, 0, 0)
  wait_seconds(0.02)
  self.set_position(3, 0, 0)
 end)
end");
            yield return null;
            yield return new WaitForSeconds(0.08f);
            Assert.That(behaviour.IsFaulted, Is.False, behaviour.LastError);
            Assert.That(owner.transform.position.x, Is.EqualTo(3));
            Assert.That(owner.GetComponent<CoroutineScheduler>().ActiveCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator DisableCancelsPendingWaitAndReenableDoesNotRestartStart()
        {
            var behaviour = Create(@"
function on_start()
 start(function() wait_seconds(0.05); refs.target.set_active(false) end)
end
function on_disable() self.set_scale(2, 2, 2) end");
            yield return null;
            yield return null;
            behaviour.enabled = false;
            Assert.That(owner.GetComponent<CoroutineScheduler>().ActiveCount, Is.Zero);
            Assert.That(owner.transform.localScale.x, Is.EqualTo(2));
            behaviour.enabled = true;
            yield return new WaitForSeconds(0.1f);
            Assert.That(target.activeSelf, Is.True);
            Assert.That(behaviour.IsFaulted, Is.False, behaviour.LastError);
        }

        [UnityTest]
        public IEnumerator DestroyCancelsWaitAndRunsOnDestroy()
        {
            Create(@"
function on_start() start(function() wait_seconds(0.05); refs.target.set_active(false) end) end
function on_destroy() refs.target.set_position(9, 0, 0) end");
            yield return null;
            Object.Destroy(owner);
            yield return new WaitForSeconds(0.1f);
            Assert.That(target.activeSelf, Is.True);
            Assert.That(target.transform.position.x, Is.EqualTo(9));
        }

        [UnityTest]
        public IEnumerator SelfDisableDefersLuaLifecycleWithoutReenteringInterpreter()
        {
            var behaviour = Create(@"
function on_start() self.set_active(false) end
function on_disable() refs.target.set_position(7, 0, 0) end");
            yield return null;
            yield return null;
            Assert.That(owner.activeSelf, Is.False);
            Assert.That(behaviour.IsFaulted, Is.False, behaviour.LastError);
            Assert.That(target.transform.position.x, Is.EqualTo(7));
        }

        [UnityTest]
        public IEnumerator RuntimeErrorFaultsAndCancelsSiblingRoutines()
        {
            LogAssert.Expect(LogType.Error, new Regex(@"\[Kimchily Lua\].*boom"));
            var behaviour = Create(@"
function on_start()
 start(function() wait_seconds(0.05); refs.target.set_active(false) end)
 start(function() error('boom') end)
end");
            yield return null;
            yield return new WaitForSeconds(0.1f);
            Assert.That(behaviour.IsFaulted, Is.True);
            Assert.That(target.activeSelf, Is.True);
            Assert.That(owner.GetComponent<CoroutineScheduler>().ActiveCount, Is.Zero);
        }

        [Test]
        public void TopLevelInfiniteLoopFaultsAtInstructionBudget()
        {
            LogAssert.Expect(LogType.Error, new Regex("Lua instruction budget exceeded"));
            var behaviour = Create("while true do end", 1000);
            Assert.That(behaviour.IsFaulted, Is.True);
        }

        [UnityTest]
        public IEnumerator UpdateInfiniteLoopFaultsAtInstructionBudget()
        {
            LogAssert.Expect(LogType.Error, new Regex("Lua instruction budget exceeded"));
            var behaviour = Create("function on_update(dt) while true do end end", 1000);
            yield return null;
            yield return null;
            Assert.That(behaviour.IsFaulted, Is.True);
        }

        [Test]
        public void HugePublicBudgetCannotDisableExecutionLimit()
        {
            LogAssert.Expect(LogType.Error, new Regex(@"Lua instruction budget exceeded \(100000\)"));
            var behaviour = Create("local n = 0; for i = 1, 100000 do n = n + 1 end", int.MaxValue);
            Assert.That(behaviour.InstructionBudget, Is.EqualTo(100000));
            Assert.That(behaviour.IsFaulted, Is.True);
        }

        [Test]
        public void HugeSerializedBudgetIsClampedAtInterpreterBoundary()
        {
            owner = new GameObject("Untrusted serialized budget");
            owner.SetActive(false);
            var behaviour = owner.AddComponent<KimchilyLuaBehaviour>();
            asset = new TextAsset("local n = 0; for i = 1, 100000 do n = n + 1 end") { name = "budget.lua" };
            behaviour.ScriptAsset = asset;
            // Deliberately bypass the public setter, as AssetBundle deserialization does.
            JsonUtility.FromJsonOverwrite("{\"instructionBudget\":2147483647}", behaviour);
            LogAssert.Expect(LogType.Error, new Regex(@"Lua instruction budget exceeded \(100000\)"));
            owner.SetActive(true);
            Assert.That(behaviour.IsFaulted, Is.True);
            Assert.That(behaviour.LastError, Does.Contain("100000"));
        }

        [UnityTest]
        public IEnumerator CoroutineInfiniteLoopFaultsAtInstructionBudget()
        {
            LogAssert.Expect(LogType.Error, new Regex("Lua instruction budget exceeded"));
            var behaviour = Create("function on_start() start(function() while true do end end) end", 1000);
            yield return null;
            yield return null;
            Assert.That(behaviour.IsFaulted, Is.True);
            Assert.That(owner.GetComponent<CoroutineScheduler>().ActiveCount, Is.Zero);
        }

        [Test]
        public void ScriptHasNoClrFilesystemDynamicLoadOrBudgetBypassLibraries()
        {
            var behaviour = Create(@"
assert(CS == nil and clr == nil and System == nil and io == nil and os == nil)
assert(load == nil and loadfile == nil and dofile == nil and require == nil)
assert(debug == nil and coroutine == nil and pcall == nil and xpcall == nil)
assert(string.gsub == nil and string.rep == nil and table.sort == nil)
self:set_position(3, 2, 1)");
            Assert.That(behaviour.IsReady, Is.True, behaviour.LastError);
            Assert.That(owner.transform.position, Is.EqualTo(new Vector3(3, 2, 1)));
        }

        [Test]
        public void SyntaxErrorIsReportedWithoutBreakingOtherBehaviours()
        {
            LogAssert.Expect(LogType.Error, new Regex(@"\[Kimchily Lua\]"));
            var behaviour = Create("function invalid(");
            Assert.That(behaviour.IsFaulted, Is.True);
            Assert.That(behaviour.LastError, Is.Not.Empty);
        }

        [UnityTest]
        public IEnumerator YieldingInLifecycleIsRejected()
        {
            LogAssert.Expect(LogType.Error, new Regex("only valid inside start"));
            var behaviour = Create("function on_start() next_frame() end");
            yield return null;
            yield return null;
            Assert.That(behaviour.IsFaulted, Is.True);
        }
    }
}
