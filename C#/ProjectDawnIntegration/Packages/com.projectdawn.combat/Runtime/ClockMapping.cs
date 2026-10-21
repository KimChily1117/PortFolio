#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectDawn.Combat
{
    public sealed class ClockMapping
    {
        public long Version { get; }
        public long OffsetUs { get; }
        public bool Trusted { get; }
        public long ExpiresUs { get; }
        public long MaxBackdateUs { get; }
        public long UncertaintyUs { get; }
        public ClockMapping(long version, long offsetUs, bool trusted, long expiresUs, long maxBackdateUs, long uncertaintyUs)
        {
            if(version<1 || expiresUs<0 || maxBackdateUs<0 || uncertaintyUs<0) throw new ArgumentOutOfRangeException(nameof(version));
            Version=version; OffsetUs=offsetUs; Trusted=trusted; ExpiresUs=expiresUs;
            MaxBackdateUs=maxBackdateUs; UncertaintyUs=uncertaintyUs;
        }
    }

    // Server-initiated, one-use challenges; client cannot supply an offset or a trust flag.
    // All methods are serialized by the server ingress route. Times are monotonic microseconds.
    public sealed class ClockSynchronizer
    {
        // 80-100ms each way also incurs scheduling/dispatch time. This is a bounded
        // measurement ceiling, not permission to backdate by an entire round trip.
        public const long MaxRoundTripUs=280000, LeaseUs=15000000, RefreshUs=5000000;
        // A minimum-RTT sample is not a bound on individual TCP/dispatch jitter.
        // Reserve 80 ms for transient scheduling/coalescing, still capped by the
        // existing 160 ms input deadline. This does not extend finality.
        public const long DispatchJitterUs=80000;
        private readonly long inputGraceUs;
        private readonly List<(long Offset,long Rtt)> samples=new List<(long,long)>();
        private string? token;
        private long sentUs, nextUs, version=1;
        private long? fixedOffset;
        public ClockMapping? Current {get;private set;}
        public long RoundTripUs {get;private set;}
        public int RejectedSamples {get;private set;}
        public ClockSynchronizer(long inputGraceUs) {this.inputGraceUs=inputGraceUs;}
        public string? Challenge(long nowUs)
        {
            if(token!=null && nowUs-sentUs<=MaxRoundTripUs) return null;
            if(token!=null) {token=null;samples.Clear();RejectedSamples++;}
            if(nowUs<nextUs) return null;
            token=Guid.NewGuid().ToString("N");sentUs=nowUs;return token;
        }
        public ClockMapping? Respond(string response,long clientReceivedUs,long clientSentUs,long nowUs)
        {
            if(token==null || response!=token) {RejectedSamples++;return null;}
            token=null;
            long rtt=nowUs-sentUs;
            if(rtt<0 || rtt>MaxRoundTripUs || clientReceivedUs<0 || clientSentUs>long.MaxValue/4 || clientSentUs<clientReceivedUs || clientSentUs-clientReceivedUs>Math.Min(10000,rtt))
            {samples.Clear();RejectedSamples++;nextUs=nowUs+1000000;return Fallback(nowUs);}
            long offset=(sentUs+rtt/2)-(clientReceivedUs+(clientSentUs-clientReceivedUs)/2);
            samples.Add((offset,rtt));nextUs=nowUs+20000;
            if(samples.Count<3) return null;
            var best=samples.OrderBy(s=>s.Rtt).First();
            decimal spread=(decimal)samples.Max(s=>s.Offset)-samples.Min(s=>s.Offset);
            long uncertainty=best.Rtt/2+5000;
            bool compatibleLease=Current!=null && Current.Trusted && nowUs<Current.ExpiresUs &&
                samples.All(s=>Math.Abs((decimal)s.Offset-Current.OffsetUs)<=Current.UncertaintyUs+10000);
            samples.Clear();nextUs=nowUs+RefreshUs;RoundTripUs=best.Rtt;
            if(fixedOffset.HasValue && Math.Abs((decimal)best.Offset-fixedOffset.Value)>uncertainty+10000)
            {RejectedSamples++;return Fallback(nowUs);}
            if(spread>20000) {
                RejectedSamples++;
                // A noisy refresh does not invalidate an unexpired, compatible estimate.
                // Keep its original version/offset/backdate bound/expiry; never extend trust
                // using noisy evidence. Retry soon, and fall back when the lease expires.
                if(compatibleLease){nextUs=nowUs+1000000;return null;}
                return Fallback(nowUs);
            }
            if(!fixedOffset.HasValue) fixedOffset=best.Offset;
            Current=new ClockMapping(++version,fixedOffset.Value,true,nowUs+LeaseUs,
                Math.Min(inputGraceUs,best.Rtt/2+DispatchJitterUs),uncertainty);
            return Current;
        }
        private ClockMapping Fallback(long nowUs)
        {Current=new ClockMapping(++version,fixedOffset??0,false,nowUs+LeaseUs,0,0);return Current;}
    }
}
