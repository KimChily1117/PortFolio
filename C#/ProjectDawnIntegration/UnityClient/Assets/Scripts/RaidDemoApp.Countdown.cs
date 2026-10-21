using UnityEngine;

namespace DnfRaidDemo {
 public sealed partial class RaidDemoApp {
  int raidEntryStage=-1;
  AudioSource raidEntryVoice;

  void PreloadCountdownArt() {
   for(int i=0;i<30;i++)RewardTexture("countdown",i);
   foreach(int i in new[]{0,1,2,7})RewardTexture("bakal_message",i);
  }

  // Original numbered frames retain their 150x150 canvas and anchors.
  // Each digit: quick arrival, readable hold, then original disintegration.
  void DrawOriginalCountdown(int number,float age,Vector2 center,float size) {
   int frame=age<.18f?Mathf.Min(2,(int)(age/.06f)):age<.70f?3+(int)((age-.18f)/.174f):6+Mathf.Min(3,(int)((age-.70f)/.075f));
   RewardArt("countdown",(3-Mathf.Clamp(number,1,3))*10+frame,new Rect(center.x-size/2,center.y-size/2,size,size));
  }

  const float MatchingCountdownSeconds=4f;
  float matchingCountdownAt=-1;
  bool MatchingCountdownActive=>matchingCountdownAt>=0 && Time.realtimeSinceStartup-matchingCountdownAt<MatchingCountdownSeconds;

  void BeginMatchingCountdown() {
   if(matchingCountdownAt>=0)return;
   matchingCountdownAt=Time.realtimeSinceStartup;raidEntryStage=-1;
   FlowRecord("MatchingCountdownBegin");
  }

  void DrawMatchingCountdown() {
   if(matchingCountdownAt<0)return;
   float elapsed=Time.realtimeSinceStartup-matchingCountdownAt;
   int stage=Mathf.Clamp((int)elapsed,0,3);
   Fill(new Rect(-1280,-800,3840,2400),new Color(0,0,0,.42f));
   if(Event.current.type==EventType.Repaint && stage>raidEntryStage) {
    raidEntryStage=stage;
    FlowRecord("MatchingCountCue stage="+stage+" age="+elapsed.ToString("F3",System.Globalization.CultureInfo.InvariantCulture));
    if(flowAuto)StartCoroutine(CaptureMatchingCue(stage));
    if(stage<3) {
     AudioClip clip;
     if(rewardSfx.TryGetValue("bk_ui_count",out clip)&&clip!=null) {
      if(raidEntryVoice==null){raidEntryVoice=gameObject.AddComponent<AudioSource>();raidEntryVoice.playOnAwake=false;}
      raidEntryVoice.Stop();raidEntryVoice.clip=clip;raidEntryVoice.volume=.65f;raidEntryVoice.Play();
     }
    }else if(raidEntryVoice!=null)raidEntryVoice.Stop();
   }
   if(stage<3) {
    Label(new Rect(300,242,680,40),"바칼 토벌 작전",25,new Color(.94f,.78f,.55f),true,TextAnchor.MiddleCenter);
    DrawOriginalCountdown(3-stage,elapsed-stage,new Vector2(640,380),270);
   } else {
    float alpha=Mathf.Clamp01((elapsed-3)/.08f);
    RewardArt("bakal_message",0,new Rect(260,307,760,116),alpha);
    RewardArt("bakal_message",1,new Rect(310,284,660,54),alpha);
    RewardArt("bakal_message",2,new Rect(310,409,660,54),alpha);
    RewardArt("bakal_message",7,new Rect(471,318,338,94),alpha);
   }
  }
  System.Collections.IEnumerator CaptureMatchingCue(int stage) {
   yield return new WaitForSecondsRealtime(.3f);yield return new WaitForEndOfFrame();
   if(flowStage=="Matching")Capture("matching-count-"+stage+".png");
  }
 }
}
