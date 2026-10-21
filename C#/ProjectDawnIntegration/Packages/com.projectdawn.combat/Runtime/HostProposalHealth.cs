namespace ProjectDawn.Combat {
 // Missing actions remain rejected. A brief loss must not terminate an entire
 // encounter; sustained lack of valid host progress still fails closed.
 public sealed class HostProposalHealth {
  public const long SilenceUs=2000000;
  public int Missing {get;private set;}
  public long LastProgressUs {get;private set;}
  public void Progress(long evaluatedUs){LastProgressUs=evaluatedUs;Missing=0;}
  public void Omitted(){Missing++;}
  public bool Failed(long finalizedUs)=>Missing>=3&&finalizedUs-LastProgressUs>=SilenceUs;
 }
}
