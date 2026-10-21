using System;
using System.Collections.Generic;
using DnfRaidDemo;
namespace ProjectDawn.Network {
 // Demo rules, not the recovered DNF style-score algorithm. Feed confirmed events only.
 public sealed class PersonalCombo {
  readonly HashSet<string> seen=new HashSet<string>();
  readonly Queue<string> order=new Queue<string>();
  public int Count {get;private set;}
  public double Damage {get;private set;}
  public int LastHitTick {get;private set;}=-100000;
  public const int TimeoutTicks=300;
  public int Rank => Count>=50?7:Count>=35?6:Count>=20?5:Count>=10?4:Count>=6?3:Count>=3?2:Count>0?1:0;
  public string RankName => new[]{"F","D","C","B","A","S","SS","SSS"}[Rank];
  public void Advance(int tick){if(tick-LastHitTick>=TimeoutTicks){Count=0;Damage=0;}}
  public bool Accept(CombatEvent e,int localSlot,int hitTick) {
   if(e.Kind!="Damage" || e.SourceId!=localSlot || e.TargetId>=0 || e.Value<=0 || string.IsNullOrEmpty(e.FeedbackKey))return false;
   if(!seen.Add(e.FeedbackKey))return false;
   order.Enqueue(e.FeedbackKey);while(order.Count>8192)seen.Remove(order.Dequeue());
   Advance(hitTick);Count++;Damage+=e.Value;LastHitTick=Math.Max(LastHitTick,hitTick);return true;
  }
  public void Reset(){seen.Clear();order.Clear();Count=0;Damage=0;LastHitTick=-100000;}
 }
 public sealed class HealthTrail {
  public float Value {get;private set;}=-1;
  float last=-1,holdUntil;
  public void Reset(){Value=last=-1;holdUntil=0;}
  public float Update(float ratio,float now,float dt) {
   ratio=Math.Max(0,Math.Min(1,ratio));
   if(last<0 || ratio>last){Value=ratio;holdUntil=0;}
   else if(ratio<last && Math.Abs(Value-last)<.001f)holdUntil=now+.3f;
   if(now>=holdUntil)Value=Math.Max(ratio,Value-Math.Max(0,dt)*.55f);
   last=ratio;return Value;
  }
 }
}
