using System;
using System.Collections;

namespace Kimchily.Creator
{
    /// <summary>Adds an explicit cleanup callback for language adapters without iterator finally support.</summary>
    public static class CoroutineRoutine
    {
        public static IEnumerator WithCleanup(IEnumerator routine, Action cleanup)
        {
            if (routine == null) throw new ArgumentNullException(nameof(routine));
            if (cleanup == null) throw new ArgumentNullException(nameof(cleanup));
            return new CleanupEnumerator(routine, cleanup);
        }

        sealed class CleanupEnumerator : IEnumerator, IDisposable
        {
            IEnumerator routine;
            Action cleanup;
            public CleanupEnumerator(IEnumerator routine, Action cleanup) { this.routine = routine; this.cleanup = cleanup; }
            public object Current => routine?.Current;
            public bool MoveNext() => routine != null && routine.MoveNext();
            public void Reset() => throw new NotSupportedException();
            public void Dispose()
            {
                var original = routine;
                var callback = cleanup;
                routine = null; cleanup = null;
                Exception error = null;
                try { (original as IDisposable)?.Dispose(); }
                catch (Exception ex) { error = ex; }
                try { callback?.Invoke(); }
                catch (Exception ex) { error = error == null ? ex : new AggregateException(error, ex); }
                if (error != null) throw error;
            }
        }
    }
}

