using System;
using System.IO;
using UnityEngine;
namespace DnfRaidDemo {
 public sealed partial class RaidDemoApp {
  void DrawJumpWarningGuide(Vector2 at,float progress,float scale=1){
   BkEffect guide;if(!fx.TryGetValue("stomp_jump_guide",out guide))return;
   var sheet=GetSheet(guide.resource,guide.width,guide.height,guide.frames);int row;
   if(sheet.ByFrame.TryGetValue(progress>=.85f?3:1,out row))DrawRow(sheet,row,at,guide.anchor[0],guide.anchor[1],scale*(progress>=.85f?1.05f:.9f),new Color(1,1,1,progress>=.85f?1:.8f));
  }
  void DrawMeteorWarning(AttackZone zone){
   float progress=Mathf.Clamp01(zone.Age/zone.Telegraph),age=zone.Age-zone.Telegraph;
   if(meteorMarkOutline==null)meteorMarkOutline=Radial(256,.975f,false);
   if(zone.Tag=="DawnMeteorShockwave"){
    var me=sim.Players[Mathf.Clamp(net.LocalPlayerId,0,sim.Players.Count-1)];Vector2 at=MarkScreenPoint(me);
    if(age<0){
     bool direct=false;foreach(var hit in sim.Zones)if((hit.Tag=="DawnMeteorImpact"||hit.Tag=="RoomMeteorImpact")&&hit.RoomId==me.RoomId&&hit.Age>=0&&hit.Age<hit.Telegraph&&RaidSimulation.Intersects(hit,me.Position,RaidSimulation.PlayerHurtboxHalfSize))direct=true;
     Color c=direct?new Color(1,.2f,.08f,.9f):new Color(.25f,.72f,1,.9f);const float w=240,h=100;
     TintTexture(meteorMarkOutline,new Rect(at.x-w/2,at.y-h/2,w,h),c);
     TintTexture(disc,new Rect(at.x-w*progress/2,at.y-h*progress/2,w*progress,h*progress),new Color(c.r,c.g,c.b,.27f));
     DrawJumpWarningGuide(at+new Vector2(0,-8),progress,.72f);
     Label(new Rect(at.x-155,at.y+54,310,23),direct?"직격 원 밖으로 이동!":progress>=.85f?"지금 점프!":"충격파 · 날개가 밝아지면 점프",15,Color.white,true,TextAnchor.MiddleCenter);
    }else if(age<.45f){float fade=1-age/.45f;TintTexture(ring,new Rect(-80,350,1440,450),new Color(1,.76f,.46f,fade*.75f));}
    return;
   }
   Vector2 p=ScreenPoint(zone.Center);float width=zone.HalfSize.x*112,height=zone.HalfSize.y*64;
   float alpha=age<0?.95f:Mathf.Clamp01(1-age/.4f);Color red=new Color(1,.11f,.035f,alpha);
   TintTexture(meteorMarkOutline,new Rect(p.x-width/2,p.y-height/2,width,height),red);
   TintTexture(disc,new Rect(p.x-width*progress/2,p.y-height*progress/2,width*progress,height*progress),new Color(1,.08f,.025f,alpha*(age<0?.3f:.55f)));
   if(age<0)Label(new Rect(p.x-130,p.y+height/2+5,260,22),"운석 직격 · 원 밖으로",14,Color.white,true,TextAnchor.MiddleCenter);
  }

  bool meteorReview,meteorReviewRoomProbe;float meteorReviewAt=-1;int meteorReviewStage;readonly System.Collections.Generic.HashSet<int> meteorReviewJumps=new System.Collections.Generic.HashSet<int>();
  readonly float[] meteorReviewTimes={.4f,1.06f,1.25f,7.1f,7.6f};
  readonly string[] meteorReviewNames={"01-meteor-fill","02-meteor-jump85","03-meteor-impact","04-phase2-room1","05-phase2-room9"};
  bool StartMeteorWarningReview(string[] args){
   if(Array.IndexOf(args,"--meteor-review")<0)return false;
   meteorReview=true;Begin(NetworkRole.Offline);PrepareDawnHud();if(dawnHud!=null)dawnHud.Display(null,false);
   sim=new RaidSimulation(2);sim.Start();sim.Boss.Phase=2;sim.Players[0].Position=new Vector2(-1,0);sim.Players[1].Position=new Vector2(2,0);sim.ForcePattern(6);
   sim.Players[0].Position=new Vector2(-6,1.8f);sim.Players[1].Position=new Vector2(-6,-1.8f);return true;
  }
  void UpdateMeteorWarningReview(){
   if(meteorReviewAt<0){meteorReviewAt=Time.unscaledTime;encounterReviewAt=meteorReviewAt;}
   float age=Mathf.Max(0,Time.unscaledTime-meteorReviewAt-1);int desired=Mathf.FloorToInt(Mathf.Min(age,6.8f)*60);
   while(sim.TickIndex<desired){var cmd=new PlayerCommand{PlayerId=0};foreach(var z in sim.Zones)if(z.Tag=="DawnMeteorShockwave"&&z.Age>=z.Telegraph*.85f&&z.Age<z.Telegraph&&meteorReviewJumps.Add(z.Id))cmd.Jump=true;sim.Tick(RaidSimulation.FixedStep,new[]{cmd});}
   if(age>=7){sim.RoomsEnabled=true;sim.Players[0].RoomId=age>=7.5f?8:0;sim.Players[0].RoomChangedTick=-1000;}
   if(age>=7&&!meteorReviewRoomProbe){meteorReviewRoomProbe=true;sim.Zones.Add(new AttackZone{Id=9999,Tag="RoomMeteorImpact",RoomId=0,Center=Vector2.zero,Shape=ZoneShape.Ellipse,HalfSize=new Vector2(3.3f,1.2f),Age=.95f,Telegraph=1.2f,ActiveTime=.12f,Recovery=1.5f});}
   foreach(var p in sim.Players){displayPositions[p.Id]=p.Position;displayHeights[p.Id]=p.JumpHeight;}
   encounterReviewTime=age;PlayEvents();UpdateSounds();UpdateCombatHud();
   if(age<8&&Time.unscaledTime>=encounterNextCapture&&!encounterCaptureBusy){encounterNextCapture=Time.unscaledTime+1f/24;StartCoroutine(CaptureEncounterFrame());}
   if(meteorReviewStage<meteorReviewTimes.Length&&age>=meteorReviewTimes[meteorReviewStage]){Capture(meteorReviewNames[meteorReviewStage]+".png");meteorReviewStage++;}
   if(age>=8.1f){bool safe=sim.Players[0].Hp==sim.Players[0].MaxHp&&meteorReviewJumps.Count==4;File.WriteAllText(Path.Combine(smokeDirectory,"frames.json"),JsonUtility.ToJson(encounterFrames,true));File.WriteAllText(Path.Combine(smokeDirectory,"meteor-review.json"),"{\"offlineInputFixture\":true,\"errors\":"+errors+",\"scenes\":"+meteorReviewStage+",\"jump85Safe\":"+safe.ToString().ToLowerInvariant()+",\"jumpCount\":"+meteorReviewJumps.Count+",\"jumpingHp\":"+sim.Players[0].Hp+",\"standingHp\":"+sim.Players[1].Hp+"}");Application.Quit(errors==0&&safe?0:1);}
  }
 }
}
