using System;
using DnfRaidDemo;
using UnityEngine;
public static class MeteorVisualChecks {
 static void Require(bool value,string reason){if(!value)throw new Exception(reason);}
 public static void Run(){
  var z=new AttackZone{Tag="RoomMeteorImpact",RoomId=3,Age=.6f,Telegraph=1.2f};
  Require(RaidMeteorVisual.Incoming(z,3)&&!RaidMeteorVisual.Incoming(z,4),"meteor warning isolated to impact room");
  z.Age=-.1f;Require(!RaidMeteorVisual.Incoming(z,3),"future queued meteor is not active warning");
  z.Age=1.2f;Require(!RaidMeteorVisual.Incoming(z,3),"warning ends at authoritative collision");
  Require(RaidMeteorVisual.Offset(1.2f,1.2f)==Vector2.zero,"fast meteor reaches exact impact position at server time");
  Require(RaidMeteorVisual.FallProgress(.8f,1.2f)==0,"fast descent waits until final 340ms");
  var half=RaidMeteorVisual.Offset(1.03f,1.2f);var end=RaidMeteorVisual.Offset(1.2f,1.2f);var start=RaidMeteorVisual.Offset(.86f,1.2f);
  Require((end-half).magnitude>(half-start).magnitude,"descent accelerates toward impact");
  Require(RaidMeteorVisual.ImpactKick(-.01f)==Vector2.zero&&RaidMeteorVisual.ImpactKick(0).magnitude>10&&RaidMeteorVisual.ImpactKick(.22f)==Vector2.zero,"camera kicks only at impact and settles within 220ms");
  Debug.Log("[METEOR_VISUAL_CHECKS_PASSED] 7 checks: room isolation, queued warning, impact boundary, fast arrival, acceleration, short camera kick");
 }
}
