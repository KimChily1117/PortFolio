using System.Collections.Generic;
using System.Linq;
namespace DnfRaidDemo {
 public partial class RaidSimulation {
  void ApplyRevives(IReadOnlyList<PlayerCommand> commands) {
   // These are server-admitted inputs with a validated host proposal. Slot order
   // deterministically arbitrates simultaneous requests for the last charge.
   if(Mode!=EncounterMode.Fighting||Elapsed>=TimeLimit||Boss.Hp<=0)return;
   if(WipeDeadlineTick>0&&TickIndex>=WipeDeadlineTick)return;
   foreach(var p in Players.OrderBy(p=>p.Id)) {
    if(ReviveCharges<=0)break;
    if(p.Hp>0||p.Departed||p.Disconnected||!commands.Any(c=>c.PlayerId==p.Id&&c.Revive))continue;
    ReviveCharges--;p.Hp=p.MaxHp*.5f;p.Invulnerable=3f;
    p.StunTime=p.AttackTime=p.DashTime=p.LungeTime=p.LungeDelay=0;
    p.JumpHeight=p.JumpVelocity=0;p.SkillLeapAge=-1;
    p.Running=false;p.ActionDuration=p.ActionElapsed=0;p.ActiveSkill=SkillId.Basic;
    System.Array.Clear(_inputBuffers[p.Id],0,_inputBuffers[p.Id].Length);
    _runBlocked[p.Id]=false;WipeDeadlineTick=0;
    Emit("Revive",p.Position,p.Hp,p.Id);
   }
  }
 }
}
