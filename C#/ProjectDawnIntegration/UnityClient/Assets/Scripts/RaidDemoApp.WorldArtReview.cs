using System;
using System.IO;
using UnityEngine;
namespace DnfRaidDemo {
 public sealed partial class RaidDemoApp {
  bool worldArtReview;int worldReviewStage=-1;float worldReviewAt;
  readonly string[] worldReviewNames={"boss-1-scale.png","boss-2-scale.png","mark-quarter.png","mark-half.png","mark-full.png"};
  bool StartWorldArtReview(string[] args){
   if(Array.IndexOf(args,"--world-art-review")<0)return false;
   worldArtReview=true;Begin(NetworkRole.Offline);sim.RoomsEnabled=true;
   worldReviewAt=Time.unscaledTime-2;return true;
  }
  void UpdateWorldArtReview(){
   // Explicit offline visual fixture. No live server state or gameplay assertion.
   if(Time.unscaledTime-worldReviewAt<1.5f)return;
   int stage=worldReviewStage+1;worldReviewAt=Time.unscaledTime;
   if(stage>=worldReviewNames.Length){Directory.CreateDirectory(smokeDirectory);File.WriteAllText(Path.Combine(smokeDirectory,"world-art-review.json"),"{\"offlineVisualFixture\":true,\"errors\":"+errors+",\"scenes\":5}");Application.Quit(errors==0?0:1);return;}
   if(stage!=worldReviewStage){
    worldReviewStage=stage;sim.TickIndex=1000;lastHudTick=sim.TickIndex;sim.Boss.Phase=stage==1?2:1;
    sim.Boss.Position=new Vector2(1.5f,0);sim.Players[0].Position=new Vector2(-2,0);
    sim.Players[0].RoomId=stage<2?4:0;sim.Players[0].RoomChangedTick=0;
    sim.MarkTargets=stage<2?Array.Empty<int>():new[]{0};sim.MarkTarget=stage<2?-1:0;
    float progress=stage==2?.25f:stage==3?.5f:.99f;
    sim.MarkDeadline=1000+Mathf.RoundToInt((1-progress)*RaidSimulation.MarkTicks);
    sim.Events.Clear();sim.Zones.Clear();sim.RoomFires.Clear();
    Capture(worldReviewNames[stage]);
   }
  }
 }
}
