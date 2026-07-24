using System.Threading;

namespace Server.Monitoring
{
    public static class CombatActivityMetrics
    {
        private static long _activeCastRecorded;

        public static long ActiveCastRecorded => Interlocked.Read(ref _activeCastRecorded);

        public static void RecordActiveCast()
        {
            Interlocked.Increment(ref _activeCastRecorded);
        }
    }
}
