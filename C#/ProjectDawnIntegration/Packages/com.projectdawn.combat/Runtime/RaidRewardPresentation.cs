using System;
namespace ProjectDawn.Combat {
 // Shared visual budget, not reward granting logic. SQL may finish before video.
 public static class RaidRewardPresentation {
  public const int CountStartMs=2200, LeadMs=5200, CardMs=1800, ReturnHoldMs=15000;
  public static int RevealMs(int count)=>LeadMs+CardMs*Math.Max(1,Math.Min(4,count));
  public static int ResultMs(int count)=>RevealMs(count)+ReturnHoldMs;
 }
}
