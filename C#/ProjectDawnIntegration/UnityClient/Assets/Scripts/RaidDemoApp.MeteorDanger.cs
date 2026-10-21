using UnityEngine;
namespace DnfRaidDemo {
 public sealed partial class RaidDemoApp {
  Texture2D meteorDangerVignette;
  void DrawMeteorRoomDanger(){
   float strength=0,remaining=999,impact=0;
   foreach(var z in sim.Zones){
    if(RaidMeteorVisual.Incoming(z,ViewedRoom)){
     float p=Mathf.Clamp01(z.Age/z.Telegraph);
     strength=Mathf.Max(strength,.30f+.50f*p*p);remaining=Mathf.Min(remaining,z.Telegraph-z.Age);
    }
    if(z.RoomId==ViewedRoom&&(z.Tag=="DawnMeteorImpact"||z.Tag=="RoomMeteorImpact")){
     float age=z.Age-z.Telegraph;
     if(age>=0&&age<.13f)impact=Mathf.Max(impact,1-age/.13f);
    }
   }
   if(strength<=0&&impact<=0)return;
   if(meteorDangerVignette==null){
    const int size=128;meteorDangerVignette=new Texture2D(size,size,TextureFormat.RGBA32,false);var pixels=new Color[size*size];
    for(int y=0;y<size;y++)for(int x=0;x<size;x++){
     float edge=Mathf.Max(Mathf.Abs((x+.5f)/size*2-1),Mathf.Abs((y+.5f)/size*2-1));
     pixels[y*size+x]=new Color(1,1,1,.10f+.90f*Mathf.Pow(edge,3));
    }
    meteorDangerVignette.SetPixels(pixels);meteorDangerVignette.Apply();meteorDangerVignette.wrapMode=TextureWrapMode.Clamp;
   }
   TintTexture(meteorDangerVignette,new Rect(0,0,1280,720),new Color(.95f,.025f,.008f,strength));
   if(impact>0)Fill(new Rect(0,0,1280,720),new Color(1,.22f,.045f,.16f*impact));
   if(strength>0){
    Fill(new Rect(420,143,440,43),new Color(.12f,0,0,.74f));
    Label(new Rect(420,143,440,43),remaining<=RaidMeteorVisual.DescentSeconds?"운석 낙하!":"이 방에 운석이 떨어집니다",23,new Color(1,.84f,.65f),true,TextAnchor.MiddleCenter);
   }
  }
  Vector2 MeteorCameraKick(){
   Vector2 kick=Vector2.zero;
   if(sim==null||sim.CinematicActive)return kick;
   foreach(var z in sim.Zones)if(z.RoomId==ViewedRoom&&(z.Tag=="DawnMeteorImpact"||z.Tag=="RoomMeteorImpact"))
    kick+=RaidMeteorVisual.ImpactKick(z.Age-z.Telegraph);
   return Vector2.ClampMagnitude(kick,18);
  }
 }
}
