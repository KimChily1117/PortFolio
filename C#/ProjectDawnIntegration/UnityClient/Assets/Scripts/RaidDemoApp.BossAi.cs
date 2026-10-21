using UnityEngine;
using DnfRaidDemo;
namespace DnfRaidDemo {
 public partial class RaidDemoApp {
  int autoChargeId=-1;
  Vector2 autoChargeEvade;
  PlayerCommand EvadeCharge(PlayerCommand command) {
   var p=sim.Players[net.LocalPlayerId];
   if(p.Invulnerable>0)return command; // Keep the skill's existing immunity; do not auto-cancel an awakening to dodge.
   if(p.RoomId!=4 || System.Array.IndexOf(sim.MarkTargets,p.Id)>=0)return command;
   var zone=sim.Zones.Find(z=>z.Tag=="BossCharge"&&z.Age<z.Telegraph+z.ActiveTime);
   if(zone==null)return command;
   Vector2 origin=zone.Offset,step=zone.Velocity*zone.ActiveTime,position=p.Position;
   float along=Mathf.Clamp01(Vector2.Dot(position-origin,step)/Mathf.Max(.001f,step.sqrMagnitude));
   if(autoChargeId!=zone.Id) {
    if((position-(origin+step*along)).magnitude>zone.Radius+1f)return command;
    var normal=new Vector2(-step.y,step.x).normalized;
    Vector2 center=origin+step*along;
    Vector2 a=center+normal*2f,b=center-normal*2f;
    a=new Vector2(Mathf.Clamp(a.x,-8,8),Mathf.Clamp(a.y,-2.4f,2.4f));b=new Vector2(Mathf.Clamp(b.x,-8,8),Mathf.Clamp(b.y,-2.4f,2.4f));
    float sa=(a-(origin+step*Mathf.Clamp01(Vector2.Dot(a-origin,step)/Mathf.Max(.001f,step.sqrMagnitude)))).magnitude;
    float sb=(b-(origin+step*Mathf.Clamp01(Vector2.Dot(b-origin,step)/Mathf.Max(.001f,step.sqrMagnitude)))).magnitude;
    autoChargeEvade=sa>=1.5f && sb>=1.5f?((a-position).sqrMagnitude<(b-position).sqrMagnitude?a:b):(sa>sb?a:b);
    autoChargeId=zone.Id;
   }
   var delta=autoChargeEvade-position;command.Move=delta.magnitude>.1f?delta.normalized:Vector2.zero;
   command.Basic=false;command.Skills=0;command.Jump=false;command.Run=true;
   command.Dash=p.AttackTime>0&&p.Cooldowns[(int)SkillId.Dash]<=0;
   return command;
  }
  void DrawChargeTrail(AttackZone zone) {
   float alpha=Mathf.Clamp01((zone.ActiveTime-zone.Age)/.45f);
   var point=ScreenPoint(zone.Center);
   OriginalWorld("world_ground_magic",point,zone.Radius*112/327f,zone.Age,new Color(1,1,1,.6f*alpha));
   OriginalWorld("world_ground_flame",point,.2f,zone.Age+zone.Id*.11f,new Color(1,1,1,.75f*alpha));
  }
  void DrawChargeLane(AttackZone zone) {
   if(zone.Age>=zone.Telegraph+zone.ActiveTime)return;
   bool active=zone.Age>=zone.Telegraph;
   var start=zone.Offset;var end=start+zone.Velocity*zone.ActiveTime;
   // A chain of ground circles describes the swept hit footprint, including both caps.
   for(int i=0;i<=24;i++) {
    var p=ScreenPoint(start+(end-start)*(i/24f));
    float radius=zone.Radius;
    TintTexture(disc,new Rect(p.x-radius*56,p.y-radius*32,radius*112,radius*64),new Color(1,.18f,.04f,active?.10f:.045f));
    if(i%4==0)TintTexture(ring,new Rect(p.x-radius*56,p.y-radius*32,radius*112,radius*64),new Color(1,.45f,.12f,.8f));
   }
   if(!active && zone.TargetId>=0 && zone.TargetId<sim.Players.Count) {
    var target=sim.Players[zone.TargetId];
    if(target.RoomId==ViewedRoom && target.Hp>0) {
     var point=ScreenPoint(target.Position);
     OriginalWorld("world_target_mark",point-new Vector2(0,185),.7f,zone.Age,Color.white);
     Label(new Rect(point.x-100,point.y-160,200,28),"▼ 돌진 표적 "+zone.HitIndex+" / 3",20,new Color(1,.25f,.15f),true,TextAnchor.MiddleCenter);
    }
   }
   var at=ScreenPoint((start+end)*.5f);
   Label(new Rect(at.x-110,at.y-28,220,24),active?"돌진!":"돌진 예고 · 옆으로 피하세요",14,new Color(1,.65f,.2f),true,TextAnchor.MiddleCenter);
  }
 }
}
