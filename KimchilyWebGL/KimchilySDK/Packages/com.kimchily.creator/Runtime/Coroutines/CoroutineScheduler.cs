using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Kimchily.Creator
{
    /// <summary>
    /// Owns coroutines on Unity's main thread. Lua/JS adapters supply IEnumerator;
    /// native Unity yield instructions retain Unity's timing and execution phases.
    /// Disabling or destroying this component cancels all work. Destroying an owner
    /// cancels its work on the next Update, even during a native Unity wait.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Kimchily/Coroutine Scheduler")]
    public sealed class CoroutineScheduler : MonoBehaviour
    {
        private sealed class Execution
        {
            internal CoroutineHandle Handle;
            internal UnityEngine.Object Owner;
            internal bool HasOwner;
            internal int OwnerId;
            internal Coroutine NativeCoroutine;
            internal readonly List<IEnumerator> Stack = new List<IEnumerator>();
            internal GuardedYield PendingYield;
            internal int ExecutionDepth;
            internal bool Cleaning;
            internal bool Finished;
        }

        // Let Unity poll custom waits in the correct phase, while keeping predicate
        // exceptions inside our fault/cleanup policy (WaitUntil, WaitWhile, etc.).
        private sealed class GuardedYield : CustomYieldInstruction
        {
            private readonly Execution execution;
            internal readonly CustomYieldInstruction Original;

            internal GuardedYield(Execution execution, CustomYieldInstruction original)
            {
                this.execution = execution;
                Original = original;
            }

            public override bool keepWaiting
            {
                get
                {
                    if (execution.Handle.IsDone)
                        return false;

                    execution.ExecutionDepth++;
                    try
                    {
                        bool waiting = Original.keepWaiting;
                        return !execution.Handle.IsDone && waiting;
                    }
                    catch (Exception exception)
                    {
                        RecordFault(execution, exception);
                        return false;
                    }
                    finally
                    {
                        execution.ExecutionDepth--;
                    }
                }
            }
        }

        private readonly Dictionary<CoroutineHandle, Execution> active =
            new Dictionary<CoroutineHandle, Execution>();
        private readonly List<Execution> ownerCheck = new List<Execution>();
        private bool acceptingWork;

        public int ActiveCount => active.Count;

        /// <summary>
        /// Starts immediately until the first yield, like Unity StartCoroutine.
        /// Pass an owner to bind the lifetime to a scene object or component.
        /// An enumerator instance must not be reused in another active coroutine.
        /// </summary>
        public CoroutineHandle StartRoutine(IEnumerator routine, UnityEngine.Object owner = null)
        {
            if (routine == null)
                throw new ArgumentNullException(nameof(routine));
            if (!acceptingWork || !isActiveAndEnabled)
                throw new InvalidOperationException("The coroutine scheduler must be active and enabled.");

            bool hasOwner = !ReferenceEquals(owner, null);
            if (hasOwner && owner == null)
                throw new ArgumentException("The coroutine owner has already been destroyed.", nameof(owner));

            var handle = new CoroutineHandle(this);
            var execution = new Execution
            {
                Handle = handle,
                Owner = owner,
                HasOwner = hasOwner,
                OwnerId = hasOwner ? owner.GetInstanceID() : 0
            };
            execution.Stack.Add(routine);
            active.Add(handle, execution);

            try
            {
                Coroutine native = StartCoroutine(Run(execution));
                // Run may finish, fault, or cancel before StartCoroutine returns.
                if (!execution.Finished)
                    execution.NativeCoroutine = native;
            }
            catch (Exception exception)
            {
                RecordFault(execution, exception);
                Finish(execution, false);
            }

            return handle;
        }

        public bool Stop(CoroutineHandle handle)
        {
            if (handle == null || !active.TryGetValue(handle, out Execution execution) || handle.IsDone)
                return false;

            handle.Status = CoroutineStatus.Cancelled;
            // Never Dispose an iterator while its MoveNext/Current/keepWaiting is
            // running. Self-cancellation unwinds when that invocation returns.
            if (execution.ExecutionDepth == 0)
                Finish(execution, true);
            return true;
        }

        public int CancelOwnedBy(UnityEngine.Object owner)
        {
            if (ReferenceEquals(owner, null))
                throw new ArgumentNullException(nameof(owner));

            int ownerId = owner.GetInstanceID();
            int cancelled = 0;
            var snapshot = new List<Execution>(active.Values);
            foreach (Execution execution in snapshot)
            {
                if (execution.HasOwner && execution.OwnerId == ownerId && Stop(execution.Handle))
                    cancelled++;
            }
            return cancelled;
        }

        /// <summary>Cancels work present at call time; teardown also rejects new work.</summary>
        public int CancelAll()
        {
            int cancelled = 0;
            var snapshot = new List<Execution>(active.Values);
            foreach (Execution execution in snapshot)
            {
                if (Stop(execution.Handle))
                    cancelled++;
            }
            return cancelled;
        }

        private void OnEnable()
        {
            acceptingWork = true;
        }

        private void Update()
        {
            ownerCheck.Clear();
            ownerCheck.AddRange(active.Values);
            foreach (Execution execution in ownerCheck)
            {
                if (execution.HasOwner && execution.Owner == null)
                    Stop(execution.Handle);
            }
            ownerCheck.Clear();
        }

        private void OnDisable()
        {
            acceptingWork = false;
            CancelAll();
        }

        private void OnDestroy()
        {
            acceptingWork = false;
            CancelAll();
        }

        private IEnumerator Run(Execution execution)
        {
            try
            {
                while (!execution.Handle.IsDone)
                {
                    if (!Advance(execution, out object yielded))
                        yield break;
                    yield return yielded;
                }
            }
            finally
            {
                if (!execution.Handle.IsDone)
                    execution.Handle.Status = CoroutineStatus.Cancelled;
                Finish(execution, false);
            }
        }

        private static bool Advance(Execution execution, out object yielded)
        {
            yielded = null;
            execution.ExecutionDepth++;
            try
            {
                DisposePendingYield(execution);
                while (!execution.Handle.IsDone && execution.Stack.Count > 0)
                {
                    IEnumerator current = execution.Stack[execution.Stack.Count - 1];
                    bool hasNext = current.MoveNext();
                    if (execution.Handle.IsDone)
                        return false;

                    if (!hasNext)
                    {
                        execution.Stack.RemoveAt(execution.Stack.Count - 1);
                        DisposeIterator(execution, current);
                        continue;
                    }

                    object next = current.Current;
                    if (execution.Handle.IsDone)
                        return false;

                    if (next is CustomYieldInstruction customYield)
                    {
                        execution.PendingYield = new GuardedYield(execution, customYield);
                        yielded = execution.PendingYield;
                        return true;
                    }

                    if (next is IEnumerator nested)
                    {
                        foreach (IEnumerator ancestor in execution.Stack)
                        {
                            if (ReferenceEquals(ancestor, nested))
                                throw new InvalidOperationException("A coroutine cannot yield itself or an active ancestor.");
                        }
                        execution.Stack.Add(nested);
                        continue;
                    }

                    // null, WaitForSeconds, WaitForFixedUpdate, WaitForEndOfFrame,
                    // AsyncOperation and Coroutine go directly to Unity.
                    yielded = next;
                    return true;
                }

                if (!execution.Handle.IsDone)
                    execution.Handle.Status = CoroutineStatus.Completed;
                return false;
            }
            catch (Exception exception)
            {
                RecordFault(execution, exception);
                return false;
            }
            finally
            {
                execution.ExecutionDepth--;
            }
        }

        private void Finish(Execution execution, bool stopNative)
        {
            if (execution.Finished || execution.Cleaning || execution.ExecutionDepth != 0)
                return;

            execution.Cleaning = true;
            if (stopNative && execution.NativeCoroutine != null)
            {
                try { StopCoroutine(execution.NativeCoroutine); }
                catch (Exception exception) { RecordFault(execution, exception); }
            }

            DisposePendingYield(execution);
            while (execution.Stack.Count > 0)
            {
                int index = execution.Stack.Count - 1;
                IEnumerator iterator = execution.Stack[index];
                execution.Stack.RemoveAt(index);
                DisposeIterator(execution, iterator);
            }

            execution.NativeCoroutine = null;
            execution.Owner = null;
            execution.Handle.Scheduler = null;
            execution.Finished = true;
            execution.Cleaning = false;
            active.Remove(execution.Handle);

            // Faults are contained at the scheduler boundary. Scripts inspect
            // handle.Exception; Unity receives one log after every finally ran.
            if (execution.Handle.Exception != null)
                Debug.LogException(execution.Handle.Exception, this);
        }

        private static void DisposePendingYield(Execution execution)
        {
            GuardedYield pending = execution.PendingYield;
            execution.PendingYield = null;
            if (pending != null)
                DisposeIterator(execution, pending.Original);
        }

        private static void DisposeIterator(Execution execution, IEnumerator iterator)
        {
            if (!(iterator is IDisposable disposable))
                return;
            try
            {
                disposable.Dispose();
            }
            catch (Exception exception)
            {
                RecordFault(execution, exception);
            }
        }

        private static void RecordFault(Execution execution, Exception exception)
        {
            Exception previous = execution.Handle.Exception;
            execution.Handle.Exception = previous == null
                ? exception
                : new AggregateException("Coroutine execution or cleanup failed.", previous, exception);
            // Cleanup errors make even a requested cancellation a fault.
            execution.Handle.Status = CoroutineStatus.Faulted;
        }
    }
}
