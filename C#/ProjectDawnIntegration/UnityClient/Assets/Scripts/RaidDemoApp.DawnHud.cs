using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
namespace DnfRaidDemo
{
    public sealed partial class RaidDemoApp
    {
        DawnHudView dawnHud;
        [Serializable] class DawnMeteorClip {public string name;public float[] times;public string[] resources,names;}
        [Serializable] class DawnMeteorClips {public DawnMeteorClip[] clips;}
        DawnMeteorClips dawnMeteorClips;
        readonly Dictionary<string,Sprite> dawnMeteorSprites=new Dictionary<string,Sprite>();
        void PrepareDawnHud()
        {
            if(dawnHud==null){dawnHud=new GameObject("Project Dawn original HUD").AddComponent<DawnHudView>();
                dawnHud.Initialize(()=>{inventoryWindow=!inventoryWindow;skillWindow=false;},()=>{skillWindow=!skillWindow;inventoryWindow=false;},arena,vignette);}
            dawnMeteorClips=JsonUtility.FromJson<DawnMeteorClips>(Resources.Load<TextAsset>("DawnOriginal/meteor-clips").text);
            foreach(var clip in dawnMeteorClips.clips)for(int i=0;i<clip.resources.Length;i++)
            {
                string key=clip.resources[i]+"#"+clip.names[i];
                if(!dawnMeteorSprites.ContainsKey(key)){
                    var sprites=Resources.LoadAll<Sprite>(clip.resources[i]);
                    dawnMeteorSprites[key]=string.IsNullOrEmpty(clip.names[i])?sprites.First():sprites.Single(x=>x.name==clip.names[i]);
                }
            }
        }
        void DrawDawnHud(PlayerState local)
        {
            DrawSkillHud(local);
            DrawDawnRoster(local);
            DrawOriginalBossHealth();
            Label(new Rect(84,80,460,25),"폭룡왕 바칼   ·   PHASE "+sim.Boss.Phase,18,new Color(1,.9f,.7f),true);
            Label(new Rect(640,80,150,25),(sim.Boss.Hp/Mathf.Max(1,sim.Boss.MaxHp)*100).ToString("0.0")+"%",20,Color.white,true,TextAnchor.MiddleRight);
            OriginalHud("bossStaggerFrame",new Rect(508,145,286,19));
            OriginalFill("bossStaggerGold",new Rect(534,148,234,10),Mathf.Clamp01(sim.Boss.Stagger/100),Color.white);
            Label(new Rect(80,144,410,20),sim.Boss.StaggerTime>0?"무력화! 지금 공격하세요":"무력화 게이지",12,sim.Boss.StaggerTime>0?cyan:gold);
            Label(new Rect(24,168,850,25),sim.Boss.PatternName+"  ·  "+PatternHint(sim.Boss.PatternName),14,gold);
            float left=Mathf.Max(0,sim.TimeLimit-sim.Elapsed);
            Label(new Rect(1090,17,150,40),((int)left/60).ToString("00")+":"+((int)left%60).ToString("00"),30,Color.white,true,TextAnchor.MiddleRight);
            Label(new Rect(910,76,345,20),net.Dawn.ClockTrusted?"연결 "+(net.Dawn.ClockRttUs/1000)+" ms · 타이밍 보정 활성":"연결 상태 확인 중 · 기본 판정",12,new Color(.65f,.8f,.85f),false,TextAnchor.MiddleRight);
            Label(new Rect(370,773,540,20),"Tab 긴급회피  ·  C 점프  ·  Space 커맨드  ·  K 스킬  ·  I 가방",12,new Color(.8f,.75f,.65f),false,TextAnchor.MiddleCenter);
        }
        Texture2D bossHudPortrait;
        void DrawOriginalBossHealth()
        {
            // Draw after the opaque IMGUI battlefield, in the same pass as the rest of the HUD.
            // The original canvas sits behind that pass; alpha was never a showcase override.
            Rect frame=new Rect(16,79,801,97.5f),fill=new Rect(83.5f,110.5f,729,31.5f);
            // Both original surrounds contain opaque black pixels. They are backgrounds,
            // never overlays: putting either over HP hides even a full health gauge.
            Fill(new Rect(75,78,725,30),new Color(.035f,.025f,.025f,.96f));
            OriginalHud("bossRaidFrame",frame);
            Fill(fill,new Color(.055f,.025f,.025f,1));
            float ratio=Mathf.Clamp01(sim.Boss.Hp/Mathf.Max(1,sim.Boss.MaxHp));
            OriginalFill("bossRaidWhite",fill,Mathf.Max(ratio,dawnHud!=null?dawnHud.BossDamageFraction:bossTrail.Value),Color.white);
            OriginalFill("bossRed",fill,ratio,Color.white);
            OriginalFill("bossRaidRed",fill,ratio,new Color(1,1,1,.65f));
            if(ratio>0)Fill(new Rect(fill.x,fill.y,fill.width*ratio,1.5f),new Color(1,.7f,.43f,.85f));
            if(bossHudPortrait==null)bossHudPortrait=Resources.Load<Texture2D>("Art/boss1/0000");
            if(bossHudPortrait!=null){
                GUI.color=Color.white;
                GUI.DrawTextureWithTexCoords(new Rect(34,106,42,39),bossHudPortrait,new Rect(235f/bossHudPortrait.width,(bossHudPortrait.height-192f)/bossHudPortrait.height,65f/bossHudPortrait.width,80f/bossHudPortrait.height));
            }
        }
        void DrawDawnRoster(PlayerState local)
        {
            // Absolute combat slots, never rotated to put the local player first.
            for(int slot=0;slot<4;slot++){
                var p=sim.Players.FirstOrDefault(player=>player.Id==slot);float x=20+slot*225;
                OriginalHud("partyFrame",new Rect(x,8,210,66));
                Fill(new Rect(x,8,218,2),slot==local.Id?cyan:new Color(.54f,.43f,.28f));
                OriginalHud("partyBase",new Rect(x+4,13,197,44));
                if(p==null){Label(new Rect(x+54,16,156,35),(slot+1)+"P  비어 있음",13,Color.gray);continue;}
                GUI.BeginGroup(new Rect(x+4,13,43,48));
                // The same layered appearance as town and combat, clipped to head/shoulders.
                int portraitFrame=art["player"].clips.First(c=>c.name=="idle").frames[0];
                DrawAvatar(p.Look,portraitFrame,new Vector2(25,185),1.6f,1,p.Hp>0?Color.white:Color.gray,false,true);
                GUI.EndGroup();
                string nickname=(p.Departed?"[퇴장] ":p.Disconnected?"[재접속 대기] ":"")+(string.IsNullOrEmpty(p.Name)?"버서커":p.Name);
                string displayName=nickname;int keep=nickname.Length;
                var nameStyle=Style(12,slot==local.Id?cyan:Color.white,true);
                while(keep>4 && nameStyle.CalcSize(new GUIContent((slot+1)+"P  "+displayName)).x>157){keep--;int head=keep/2;displayName=nickname.Substring(0,head)+"…"+nickname.Substring(nickname.Length-(keep-head));}
                GUI.Label(new Rect(x+54,12,157,20),new GUIContent((slot+1)+"P  "+displayName,nickname),nameStyle);
                HealthBar(new Rect(x+54,36,156,9),p.Hp/Mathf.Max(1,p.MaxHp),playerTrails[p.Id].Value,p.Hp>0?new Color(.85f,.18f,.2f):Color.gray);
                Label(new Rect(x+54,46,156,17),p.Hp<=0?"전투 불능":Mathf.CeilToInt(p.Hp/p.MaxHp*100)+"%  "+Mathf.CeilToInt(p.Hp)+" / "+Mathf.CeilToInt(p.MaxHp)+"  ·  방 "+(p.RoomId+1),11,new Color(.82f,.82f,.85f));
            }
        }
        void DrawDawnMeteor(AttackZone zone,Vector2 point)
        {
            if(dawnMeteorClips==null || (zone.Tag!="DawnMeteorImpact" && zone.Tag!="RoomMeteorImpact"))return;
            bool falling=zone.Age<zone.Telegraph;
            float time=falling?zone.Age:zone.Age-zone.Telegraph;
            var fallingPoint=point+RaidMeteorVisual.Offset(zone.Age,zone.Telegraph);
            if(falling&&RaidMeteorVisual.FallProgress(zone.Age,zone.Telegraph)>0){
                var savedColor=GUI.color;
                for(int trail=2;trail>=1;trail--){GUI.color=new Color(1,.52f,.25f,trail==2?.12f:.24f);MeteorSprite(1,time,point+RaidMeteorVisual.Offset(zone.Age-trail*.018f,zone.Telegraph),.4f*DawnPatternRules.VisualScale,true);}
                GUI.color=savedColor;
                MeteorSprite(1,time*1.8f,fallingPoint,.4f*DawnPatternRules.VisualScale,true);MeteorSprite(0,time*1.8f,fallingPoint,.4f*DawnPatternRules.VisualScale,true);
            }
            else if(!falling){MeteorSprite(2,time*1.3f,point,1.1f,false);if(time<.24f)OriginalWorld("world_impact_wave",point,1.1f,time,Color.white,false,.24f);}
            if(zone.Age<.5f)MeteorSprite(3,zone.Age,point,.55f,false);
        }
        void MeteorSprite(int index,float time,Vector2 anchor,float scale,bool loop)
        {
            var clip=dawnMeteorClips.clips[index];
            if(loop)time%=clip.times[clip.times.Length-1]+1f/12;
            int frame=0;for(int i=0;i<clip.times.Length;i++)if(clip.times[i]<=time)frame=i;
            var sprite=dawnMeteorSprites[clip.resources[frame]+"#"+clip.names[frame]];var texture=sprite.texture;var uv=sprite.textureRect;
            var box=new Rect(anchor.x-sprite.pivot.x*scale,anchor.y-(sprite.rect.height-sprite.pivot.y)*scale,sprite.rect.width*scale,sprite.rect.height*scale);
            GUI.DrawTextureWithTexCoords(box,texture,new Rect(uv.x/texture.width,uv.y/texture.height,uv.width/texture.width,uv.height/texture.height));
        }
    }
}
