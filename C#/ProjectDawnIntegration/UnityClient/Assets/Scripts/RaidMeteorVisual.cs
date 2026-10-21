using UnityEngine;
namespace DnfRaidDemo {
 // Pure presentation functions: authoritative activation remains zone.Age == zone.Telegraph.
 public static class RaidMeteorVisual {
  public const float DescentSeconds=.34f;
  public static bool Incoming(AttackZone z,int room)=>z.RoomId==room&&
   (z.Tag=="DawnMeteorImpact"||z.Tag=="RoomMeteorImpact")&&z.Age>=0&&z.Age<z.Telegraph;
  public static float FallProgress(float age,float telegraph)=>Mathf.Clamp01(1-(telegraph-age)/DescentSeconds);
  public static Vector2 Offset(float age,float telegraph){float p=FallProgress(age,telegraph);return new Vector2(780,-920)*(1-p*p);}
  public static Vector2 ImpactKick(float age){
   if(age<0||age>=.22f)return Vector2.zero;
   float amp=1-age/.22f;amp*=amp;
   // Immediate kick, then two short reversals instead of a long sinusoidal float.
   float x=age<.035f?-1:age<.075f?.65f:age<.12f?-.35f:.12f;
   float y=age<.035f?1:age<.075f?-.6f:age<.12f?.3f:-.1f;
   return new Vector2(x*11,y*15)*amp;
  }
 }
}
