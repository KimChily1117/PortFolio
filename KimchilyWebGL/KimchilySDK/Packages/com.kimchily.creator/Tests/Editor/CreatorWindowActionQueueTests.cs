using System;
using System.Collections.Generic;
using Kimchily.Creator.Editor;
using NUnit.Framework;

namespace Kimchily.Creator.Tests
{
    public sealed class CreatorWindowActionQueueTests
    {
        [Test]
        public void ButtonRequestOnlySchedulesWorkUntilTheGuiEventHasFinished()
        {
            var callbacks = new List<Action>();
            bool insideGui = true;
            int calls = 0;
            Exception failure = null;
            using (var queue = new CreatorWindowActionQueue(callbacks.Add, action => callbacks.Remove(action), null, error => failure = error))
            {
                Assert.IsTrue(queue.TryEnqueue(() => { Assert.IsFalse(insideGui); calls++; }));
                Assert.AreEqual(0, calls, "The button event must never run validation/build/publish inline.");
                Assert.IsTrue(queue.IsBusy);
                insideGui = false;
                callbacks[0]();
                Assert.IsNull(failure);
                Assert.AreEqual(1, calls);
                Assert.IsFalse(queue.IsBusy);
                Assert.IsEmpty(callbacks);
            }
        }

        [Test]
        public void DuplicateRequestsAreBlockedWhilePendingAndWhileExecuting()
        {
            var callbacks = new List<Action>();
            int calls = 0;
            using (var queue = new CreatorWindowActionQueue(callbacks.Add, action => callbacks.Remove(action), null, error => Assert.Fail(error.ToString())))
            {
                Assert.IsTrue(queue.TryEnqueue(() =>
                {
                    calls++;
                    Assert.IsTrue(queue.IsBusy);
                    Assert.IsFalse(queue.TryEnqueue(() => calls++), "A build callback must not allow a reentrant second request.");
                }));
                Assert.IsFalse(queue.TryEnqueue(() => calls++));
                Assert.AreEqual(1, callbacks.Count);
                callbacks[0]();
                Assert.AreEqual(1, calls);
                Assert.IsFalse(queue.IsBusy);
            }
        }

        [Test]
        public void ClosingTheWindowCancelsEvenAnAlreadyCapturedEditorCallback()
        {
            var callbacks = new List<Action>();
            int calls = 0;
            var queue = new CreatorWindowActionQueue(callbacks.Add, action => callbacks.Remove(action), null, error => Assert.Fail(error.ToString()));
            Assert.IsTrue(queue.TryEnqueue(() => calls++));
            Action editorSnapshot = callbacks[0];
            queue.Dispose();
            Assert.IsEmpty(callbacks);
            editorSnapshot(); // delayCall may already have taken a copy of its invocation list.
            Assert.AreEqual(0, calls);
            Assert.IsFalse(queue.IsBusy);
            Assert.IsFalse(queue.TryEnqueue(() => calls++));
        }

        [Test]
        public void AFailedActionReportsTheErrorAndAllowsAnExplicitRetry()
        {
            var callbacks = new List<Action>();
            var expected = new InvalidOperationException("Build failed");
            Exception failure = null;
            int completed = 0;
            using (var queue = new CreatorWindowActionQueue(callbacks.Add, action => callbacks.Remove(action), null, error => failure = error))
            {
                queue.TryEnqueue(() => throw expected);
                callbacks[0]();
                Assert.AreSame(expected, failure);
                Assert.IsFalse(queue.IsBusy);
                Assert.IsTrue(queue.TryEnqueue(() => completed++));
                callbacks[0]();
                Assert.AreEqual(1, completed);
            }
        }

        [Test]
        public void ClosingDuringExecutionDoesNotNotifyOrScheduleTheClosedWindowAgain()
        {
            var callbacks = new List<Action>();
            bool closed = false;
            int lateNotifications = 0;
            using (var queue = new CreatorWindowActionQueue(callbacks.Add, action => callbacks.Remove(action),
                () => { if (closed) lateNotifications++; }, error => Assert.Fail(error.ToString())))
            {
                queue.TryEnqueue(() => { closed = true; queue.Dispose(); });
                callbacks[0]();
                Assert.AreEqual(0, lateNotifications);
                Assert.IsFalse(queue.IsBusy);
                Assert.IsFalse(queue.TryEnqueue(() => Assert.Fail("Closed window executed another action.")));
            }
        }
    }
}
