using System;
using System.Collections.Generic;
using UnityEngine;
namespace DnfRaidDemo {
 public sealed partial class RaidDemoApp {
  [Serializable]class OriginalHudItem {public string id,resource;public int width,height;}
  [Serializable]class OriginalHudDocument {public OriginalHudItem[] textures;}
  [Serializable]class OriginalWorldDocument {public BkEffect[] effects;}
  readonly Dictionary<string,Texture2D> originalHud=new Dictionary<string,Texture2D>();
  bool originalSkinReady;
  Texture2D originalCorridor;
  Sprite originalSeria;
  void ApplyOriginalTownArt(){
   if(originalSeria==null){var texture=Resources.Load<Texture2D>("OriginalWorldEffects/seria_room_exact");if(texture==null)return;originalSeria=UnityEngine.Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),100);}
   foreach(var renderer in FindObjectsOfType<SpriteRenderer>())if(renderer.gameObject.name=="Square")renderer.sprite=originalSeria;
  }
  void LoadOriginalCombatArt(){
   var text=Resources.Load<TextAsset>("OriginalCombatHud/manifest");
   if(text!=null)foreach(var item in JsonUtility.FromJson<OriginalHudDocument>(text.text).textures){
    var bytes=Resources.Load<TextAsset>(item.resource);if(bytes==null)continue;
    var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);texture.LoadImage(bytes.bytes);texture.name=item.id;texture.filterMode=FilterMode.Bilinear;texture.wrapMode=TextureWrapMode.Clamp;originalHud[item.id]=texture;
   }
   text=Resources.Load<TextAsset>("OriginalWorldEffects/manifest");
   if(text!=null)foreach(var effect in JsonUtility.FromJson<OriginalWorldDocument>(text.text).effects){fx[effect.id]=effect;GetSheet(effect.resource,effect.width,effect.height,effect.frames);}
   originalCorridor=Resources.Load<Texture2D>("OriginalWorldEffects/bakal_side_room");
   text=Resources.Load<TextAsset>("DawnRewards/Epic/manifest");
   if(text!=null)foreach(var effect in JsonUtility.FromJson<OriginalWorldDocument>(text.text).effects){fx[effect.id]=effect;GetSheet(effect.resource,effect.width,effect.height,effect.frames);}
  }
  bool OriginalHud(string id,Rect box,Color? tint=null){Texture2D texture;if(!originalHud.TryGetValue(id,out texture))return false;TintTexture(texture,box,tint??Color.white);return true;}
  void OriginalFill(string id,Rect box,float fraction,Color tint,bool vertical=false){
   Texture2D texture;if(!originalHud.TryGetValue(id,out texture))return;fraction=Mathf.Clamp01(fraction);
   GUI.color=tint;
   if(vertical)GUI.DrawTextureWithTexCoords(new Rect(box.x,box.y+box.height*(1-fraction),box.width,box.height*fraction),texture,new Rect(0,0,1,fraction));
   else GUI.DrawTextureWithTexCoords(new Rect(box.x,box.y,box.width*fraction,box.height),texture,new Rect(0,0,fraction,1));
   GUI.color=Color.white;
  }
  void ConfigureOriginalSkin(){
   GUI.skin.font=font;if(originalSkinReady)return;originalSkinReady=true;
   var button=GUI.skin.button;button.font=font;button.fontSize=15;button.border=new RectOffset(6,6,5,5);
   button.normal.background=OriginalWindowTexture("chrome",66);button.hover.background=OriginalWindowTexture("chrome",67);button.active.background=OriginalWindowTexture("chrome",67);button.focused.background=button.hover.background;
   foreach(var state in new[]{button.normal,button.hover,button.active,button.focused})state.textColor=Color.white;
   foreach(var field in new[]{GUI.skin.textField,GUI.skin.textArea}){field.font=font;field.fontSize=17;field.normal.background=OriginalWindowTexture("chrome",57);field.focused.background=field.normal.background;field.hover.background=field.normal.background;field.border=new RectOffset(3,3,3,3);field.padding=new RectOffset(8,8,4,4);field.normal.textColor=Color.white;field.focused.textColor=Color.white;}
   foreach(var style in GUI.skin.customStyles)style.font=font;
  }
  bool DrawOriginalSkillHud(PlayerState p){
   if(!originalHud.ContainsKey("bottomFrame"))return false;
   OriginalHud("bottomFade",new Rect(0,696,1280,104));
   OriginalHud("bottomFrame",new Rect(262,628,756,172));
   Rect hpBox=new Rect(288,660,108,108),mpBox=new Rect(884,660,108,108);
   OriginalHud("hpOrb",hpBox,new Color(.16f,.16f,.16f));OriginalHud("mpOrb",mpBox,new Color(.16f,.16f,.16f));
   float hpFraction=p.Hp/Mathf.Max(1,p.MaxHp),mpFraction=p.Mp/Mathf.Max(1,p.MaxMp);
   OriginalFill("orbDamageMask",hpBox,Mathf.Max(hpFraction,playerTrails[p.Id].Value),Color.white,true);
   OriginalFill("hpOrb",hpBox,hpFraction,Color.white,true);OriginalFill("mpOrb",mpBox,mpFraction,Color.white,true);
   // Fill only the inner circles, leaving the original decorative rims intact.
   for(int i=0;i<slots.Length;i++)DrawSlot(SlotRect(i),slots[i],p,SkillBook.SlotKeys[i]);
   float x=459;if(p.FrenzyTime>0)DrawBuff(ref x,SkillId.Frenzy,p.FrenzyTime);if(p.BerserkTime>0)DrawBuff(ref x,SkillId.Berserk,p.BerserkTime);if(p.OverkillTime>0)DrawBuff(ref x,SkillId.ExtremeOverkill,p.OverkillTime);
   Label(hpBox,Mathf.RoundToInt(hpFraction*100)+"%",19,Color.white,true,TextAnchor.MiddleCenter);
   Label(mpBox,Mathf.FloorToInt(mpFraction*100)+"%",19,Color.white,true,TextAnchor.MiddleCenter);
   Label(new Rect(286,750,112,22),Mathf.CeilToInt(p.Hp)+" / "+Mathf.CeilToInt(p.MaxHp),12,Color.white,true,TextAnchor.MiddleCenter);
   if(GUI.Button(new Rect(1040,748,82,28),"I 가방"))ToggleOriginalInventory(false);
   if(GUI.Button(new Rect(1130,748,82,28),"K 스킬")){skillWindow=!skillWindow;inventoryWindow=false;}
   return true;
  }
  bool OriginalWorld(string id,Vector2 at,float scale,float age,Color tint,bool loop=true,float duration=0){
   if(!fx.ContainsKey(id))return false;DrawEffect(id,age,at,scale,1,tint,loop,duration);return true;
  }
 }
}
