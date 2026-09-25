using System;
using UnityEngine;

namespace Kimchily.Creator
{
    public enum CoroutineStatus
    {
        Running,
        Completed,
        Cancelled,
        Faulted
    }

    /// <summary>A script-language-independent coroutine result. May itself be yielded.</summary>
    public sealed class CoroutineHandle : CustomYieldInstruction
    {
        internal CoroutineScheduler Scheduler;

        internal CoroutineHandle(CoroutineScheduler scheduler)
        {
            Scheduler = scheduler;
            Status = CoroutineStatus.Running;
        }

        public CoroutineStatus Status { get; internal set; }
        public Exception Exception { get; internal set; }
        public bool IsDone => Status != CoroutineStatus.Running;
        public override bool keepWaiting => !IsDone;

        /// <summary>Returns true only when this call requests cancellation of running work.</summary>
        public bool Cancel()
        {
            return Scheduler != null && Scheduler.Stop(this);
        }
    }
}
