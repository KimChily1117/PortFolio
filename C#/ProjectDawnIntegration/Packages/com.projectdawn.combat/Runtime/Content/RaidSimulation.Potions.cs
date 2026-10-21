using System.Collections.Generic;
using System.Linq;
namespace DnfRaidDemo {
 public partial class RaidSimulation {
  void ApplyPotions(IReadOnlyList<PlayerCommand> commands) {
   // Inventory, heal amount and cooldown are server-replayed state, never client values.
   foreach(var p in Players) {
    if(p.Hp<=0 || p.Hp>=p.MaxHp || p.Departed || p.Disconnected || p.PotionCharges<=0 || TickIndex<p.NextPotionTick)continue;
    if(!commands.Any(c=>c.PlayerId==p.Id && c.Potion))continue;
    float before=p.Hp;
    p.Hp=System.Math.Min(p.MaxHp,p.Hp+p.MaxHp*PotionHealFraction);
    p.PotionCharges--;p.NextPotionTick=TickIndex+PotionCooldownTicks;
    Emit("Heal",p.Position,p.Hp-before,p.Id);
   }
  }
 }
}
