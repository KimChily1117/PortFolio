using UnityEngine;

namespace DnfRaidDemo {
 public sealed partial class RaidDemoApp {
  // Presentation-only fall: shared cinematic time, unchanged authoritative positions/rooms.
  // User revision: only fast vertical background during descent; actors return at landing.
  const float PlayerFallStart=6.7f, PlayerFallReveal=8f, PlayerFallLand=8.35f;
  void DrawPhaseTwoPlayerFall(float time){
   if(time<PlayerFallStart)return;
   if(time<PlayerFallReveal){
    Fill(new Rect(0,0,1280,720),new Color(.075f,.055f,.095f));
    float travel=(time-PlayerFallStart)*850;
    // Background moves UP as the camera follows the falling party down the tower.
    for(int row=-1;row<3;row++)for(int col=-1;col<4;col++){
     float y=row*430-travel%430;
     PhaseTwoFrame("fall_background",row%2==0?0:2,new Vector2(col*430,y),1.85f,new Color(.55f,.44f,.58f,.85f));
    }
    for(int n=0;n<24;n++){
     float y=Mathf.Repeat(n*73-travel*1.75f,900)-100;
     Fill(new Rect((n*137+41)%1280,y,2+n%3,95+n%5*24),new Color(.56f,.38f,.44f,.09f));
    }
    Fill(new Rect(0,0,1280,720),new Color(0,0,0,Mathf.Clamp01((PlayerFallStart+.18f-time)/.18f)));
   }
   if(time<PlayerFallLand)return;
   foreach(var player in sim.Players){
    if(player.Departed||player.RoomId!=ViewedRoom)continue;
    Vector2 ground=ScreenPoint(player.Position);
    Vector2 foot=ground;
    float landed=time-PlayerFallLand;
    string clip=landed<.24f?"land":"idle";
    float poseTime=landed<.24f?landed*.42f:landed-.24f;
    var pose=new RaidMotion.Pose(clip,poseTime,clip!="idle",player.Facing);
    if(landed>=0)OriginalWorld("world_actor_shadow",ground,.4f,0,Color.white);
    DrawAvatar(player.Look,PlayerFrame(pose),foot,1.15f,pose.Facing,Color.white,RaidFrenzy.SwordVisible(player),false);
    if(landed>=0&&landed<1.1f){
     EncounterEffect("landing_a",landed,ground,1.2f,.65f);
     EncounterEffect("landing_b",landed,ground,1.2f,.9f);
     OriginalWorld("world_impact_wave",ground,.65f,landed,Color.white,false,.48f);
    }
   }
  }
 }
}
