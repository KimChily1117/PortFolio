using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Kimchily.Creator.Tests
{
    public sealed class CoroutineSchedulerTests
    {
        private GameObject host;
        private CoroutineScheduler scheduler;
        private readonly List<GameObject> owners = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Coroutine scheduler test");
            scheduler = host.AddComponent<CoroutineScheduler>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (host != null)
                UnityEngine.Object.Destroy(host);
            foreach (GameObject owner in owners)
            {
                if (owner != null)
                    UnityEngine.Object.Destroy(owner);
            }
            owners.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator NullYieldResumesOnLaterFrameAndCompletes()
        {
            int startedFrame = -1;
            int resumedFrame = -1;
            IEnumerator Routine()
            {
                startedFrame = Time.frameCount;
                yield return null;
                resumedFrame = Time.frameCount;
            }

            CoroutineHandle handle = scheduler.StartRoutine(Routine());
            Assert.AreEqual(Time.frameCount, startedFrame);
            Assert.AreEqual(CoroutineStatus.Running, handle.Status);
            yield return handle;

            Assert.Greater(resumedFrame, startedFrame);
            Assert.AreEqual(CoroutineStatus.Completed, handle.Status);
            Assert.IsNull(handle.Exception);
            Assert.AreEqual(0, scheduler.ActiveCount);
        }

        [Test]
        public void ImmediateCompletionDoesNotRetainAnActiveNativeCoroutine()
        {
            IEnumerator Routine() { yield break; }
            CoroutineHandle handle = scheduler.StartRoutine(Routine());
            Assert.AreEqual(CoroutineStatus.Completed, handle.Status);
            Assert.AreEqual(0, scheduler.ActiveCount);
            Assert.IsFalse(handle.Cancel());
        }

        [Test]
        public void CancellationDisposesNestedIteratorsInsideOutExactlyOnce()
        {
            var disposed = new List<string>();
            IEnumerator Child()
            {
                try { yield return new WaitForSeconds(60); }
                finally { disposed.Add("child"); }
            }
            IEnumerator Parent()
            {
                try { yield return Child(); }
                finally { disposed.Add("parent"); }
            }

            CoroutineHandle handle = scheduler.StartRoutine(Parent());
            Assert.IsTrue(handle.Cancel());
            Assert.IsFalse(handle.Cancel());
            CollectionAssert.AreEqual(new[] { "child", "parent" }, disposed);
            Assert.AreEqual(CoroutineStatus.Cancelled, handle.Status);
            Assert.AreEqual(0, scheduler.ActiveCount);
        }

        [UnityTest]
        public IEnumerator SelfCancellationWaitsForMoveNextToReturnBeforeDisposal()
        {
            CoroutineHandle handle = null;
            bool disposed = false;
            bool cancellationAccepted = false;
            bool disposedInsideMoveNext = false;
            IEnumerator Routine()
            {
                try
                {
                    yield return null;
                    cancellationAccepted = handle.Cancel();
                    disposedInsideMoveNext = disposed;
                    yield return null;
                }
                finally { disposed = true; }
            }

            handle = scheduler.StartRoutine(Routine());
            yield return handle;

            Assert.IsTrue(cancellationAccepted);
            Assert.IsFalse(disposedInsideMoveNext);
            Assert.IsTrue(disposed);
            Assert.AreEqual(CoroutineStatus.Cancelled, handle.Status);
            Assert.AreEqual(0, scheduler.ActiveCount);
        }

        [UnityTest]
        public IEnumerator NestedFaultIsCapturedAndDisposesTheParent()
        {
            var disposed = new List<string>();
            IEnumerator Child()
            {
                try
                {
                    yield return null;
                    throw new InvalidOperationException("nested failure");
                }
                finally { disposed.Add("child"); }
            }
            IEnumerator Parent()
            {
                try { yield return Child(); }
                finally { disposed.Add("parent"); }
            }

            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: nested failure"));
            CoroutineHandle handle = scheduler.StartRoutine(Parent());
            yield return handle;

            Assert.AreEqual(CoroutineStatus.Faulted, handle.Status);
            Assert.IsInstanceOf<InvalidOperationException>(handle.Exception);
            CollectionAssert.AreEqual(new[] { "child", "parent" }, disposed);
            Assert.AreEqual(0, scheduler.ActiveCount);
        }

        [UnityTest]
        public IEnumerator CustomYieldPredicateFaultUsesTheSameCleanupPolicy()
        {
            bool disposed = false;
            IEnumerator Routine()
            {
                try
                {
                    yield return new WaitUntil(() => throw new InvalidOperationException("predicate failure"));
                }
                finally { disposed = true; }
            }

            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: predicate failure"));
            CoroutineHandle handle = scheduler.StartRoutine(Routine());
            yield return handle;

            Assert.AreEqual(CoroutineStatus.Faulted, handle.Status);
            Assert.IsTrue(disposed);
            Assert.AreEqual(0, scheduler.ActiveCount);
        }

        [Test]
        public void CleanupFaultDoesNotPreventOtherIteratorsFromBeingDisposed()
        {
            bool parentDisposed = false;
            IEnumerator Child()
            {
                try { yield return new WaitForSeconds(60); }
                finally { throw new InvalidOperationException("cleanup failure"); }
            }
            IEnumerator Parent()
            {
                try { yield return Child(); }
                finally { parentDisposed = true; }
            }

            CoroutineHandle handle = scheduler.StartRoutine(Parent());
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: cleanup failure"));
            handle.Cancel();

            Assert.AreEqual(CoroutineStatus.Faulted, handle.Status);
            Assert.IsTrue(parentDisposed);
            Assert.AreEqual(0, scheduler.ActiveCount);
        }

        [UnityTest]
        public IEnumerator DestroyedOwnerCancelsDuringLongNativeWait()
        {
            GameObject owner = NewOwner();
            bool disposed = false;
            IEnumerator Routine()
            {
                try { yield return new WaitForSeconds(60); }
                finally { disposed = true; }
            }

            CoroutineHandle handle = scheduler.StartRoutine(Routine(), owner);
            UnityEngine.Object.Destroy(owner);
            yield return null;
            yield return null;

            Assert.AreEqual(CoroutineStatus.Cancelled, handle.Status);
            Assert.IsTrue(disposed);
            Assert.AreEqual(0, scheduler.ActiveCount);
        }

        [Test]
        public void OwnerCancellationLeavesOtherOwnersRunning()
        {
            GameObject firstOwner = NewOwner();
            GameObject secondOwner = NewOwner();
            IEnumerator Routine() { yield return new WaitForSeconds(60); }
            CoroutineHandle first = scheduler.StartRoutine(Routine(), firstOwner);
            CoroutineHandle second = scheduler.StartRoutine(Routine(), secondOwner);

            Assert.AreEqual(1, scheduler.CancelOwnedBy(firstOwner));
            Assert.AreEqual(CoroutineStatus.Cancelled, first.Status);
            Assert.AreEqual(CoroutineStatus.Running, second.Status);
            Assert.AreEqual(1, scheduler.ActiveCount);
        }

        [Test]
        public void DisablingSchedulerCancelsAndDisposesWork()
        {
            bool disposed = false;
            IEnumerator Routine()
            {
                try { yield return new WaitForSeconds(60); }
                finally { disposed = true; }
            }

            CoroutineHandle handle = scheduler.StartRoutine(Routine());
            scheduler.enabled = false;

            Assert.AreEqual(CoroutineStatus.Cancelled, handle.Status);
            Assert.IsTrue(disposed);
            Assert.AreEqual(0, scheduler.ActiveCount);
            Assert.Throws<InvalidOperationException>(() => scheduler.StartRoutine(Routine()));
        }

        [UnityTest]
        public IEnumerator DestroyingSchedulerCancelsAndDisposesWork()
        {
            bool disposed = false;
            IEnumerator Routine()
            {
                try { yield return new WaitForSeconds(60); }
                finally { disposed = true; }
            }

            CoroutineHandle handle = scheduler.StartRoutine(Routine());
            UnityEngine.Object.Destroy(host);
            yield return null;

            Assert.AreEqual(CoroutineStatus.Cancelled, handle.Status);
            Assert.IsTrue(disposed);
        }

        [UnityTest]
        public IEnumerator RealtimeWaitCompletesWithTimeScaleZero()
        {
            float previousTimeScale = Time.timeScale;
            bool resumed = false;
            IEnumerator Routine()
            {
                yield return new WaitForSecondsRealtime(0.01f);
                resumed = true;
            }

            try
            {
                Time.timeScale = 0;
                CoroutineHandle handle = scheduler.StartRoutine(Routine());
                yield return handle;
                Assert.IsTrue(resumed);
                Assert.AreEqual(CoroutineStatus.Completed, handle.Status);
            }
            finally { Time.timeScale = previousTimeScale; }
        }

        private GameObject NewOwner()
        {
            var owner = new GameObject("Coroutine owner test");
            owners.Add(owner);
            return owner;
        }
    }
}
