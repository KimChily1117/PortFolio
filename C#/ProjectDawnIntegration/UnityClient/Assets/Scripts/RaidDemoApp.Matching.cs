using System;
using System.IO;
using System.Linq;
using System.Collections;
using Google.Protobuf.Protocol;
using UnityEngine;
using UnityEngine.UI;

namespace DnfRaidDemo {
 public sealed partial class RaidDemoApp {
  LobbyPlayerInfo[] matchingMembers=new LobbyPlayerInfo[0];
  RectTransform[] matchingSlots;
  bool matchingPreview;

  void UpdateFlowParty(LobbyPlayerInfo[] members) {
   matchingMembers=members.Select(m=>m.Clone()).ToArray();
   if(flowParty==null) {
    flowParty=new GameObject("Dawn original party UI",typeof(Canvas),typeof(CanvasScaler));
    var canvas=flowParty.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=5;
    var scaler=flowParty.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
    // The shipped popup is 1920x1080, not 1280x800. Expand keeps every slot inside
    // the viewport at either aspect, and responds to live window resizes.
    scaler.referenceResolution=new Vector2(1920,1080);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
    var panel=Instantiate(Resources.Load<GameObject>("DawnOriginal/PartyPopUp"),flowParty.transform,false);
    var rect=panel.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=Vector2.zero;
    rect.sizeDelta=new Vector2(1920,1080);rect.localScale=Vector3.one;
    var images=panel.GetComponentsInChildren<Image>(true);
    foreach(var name in new[]{"BackGround","Panel"}) {
     var image=images.FirstOrDefault(i=>i.name==name);if(image==null)continue;
     var rt=image.rectTransform;rt.SetParent(flowParty.transform,false);rt.SetSiblingIndex(name=="BackGround"?0:1);
     rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=rt.offsetMax=Vector2.zero;
     if(name=="BackGround"&&image.sprite!=null) {
      var fit=image.gameObject.AddComponent<AspectRatioFitter>();fit.aspectMode=AspectRatioFitter.AspectMode.EnvelopeParent;
      fit.aspectRatio=image.sprite.rect.width/image.sprite.rect.height;
     }
     image.raycastTarget=false;
    }
    matchingSlots=Enumerable.Range(1,4).Select(i=>panel.GetComponentsInChildren<RectTransform>(true).Single(t=>t.name=="Slot_"+i)).ToArray();
    foreach(var tmp in panel.GetComponentsInChildren<TMPro.TMP_Text>(true)) {
     if(tmp.name.Contains("PlayerName")){tmp.rectTransform.anchoredPosition=new Vector2(0,370);tmp.rectTransform.sizeDelta=new Vector2(400,60);}
     tmp.enabled=false;var go=new GameObject(tmp.name+"_Text",typeof(RectTransform),typeof(Text));go.transform.SetParent(tmp.transform,false);
     var rt=go.GetComponent<RectTransform>();rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=rt.offsetMax=Vector2.zero;
     var label=go.GetComponent<Text>();label.font=font;label.fontSize=tmp.name=="Title"?50:28;label.alignment=TextAnchor.MiddleCenter;label.color=Color.white;
     label.horizontalOverflow=HorizontalWrapMode.Wrap;label.resizeTextForBestFit=true;label.resizeTextMinSize=16;label.resizeTextMaxSize=label.fontSize;
     label.text=tmp.name=="Title"?"바칼 레이드 · 매칭 대기":"대기 중";
    }
    foreach(var b in panel.GetComponentsInChildren<Button>())b.interactable=false;
   }
   for(int i=0;i<4;i++) {
    var label=flowParty.GetComponentsInChildren<Text>().FirstOrDefault(x=>x.name=="Slot_"+(i+1)+"_PlayerName_Text");
    if(label!=null){label.text=i<members.Length?members[i].Name:i<flowPartySize?"참가자 대기 중":"미사용 슬롯";label.color=i<members.Length?new Color(1,.88f,.65f):Color.gray;}
    matchingSlots[i].GetComponent<Image>().color=i<flowPartySize?Color.white:new Color(1,1,1,.22f);
   }
   var status=flowParty.GetComponentsInChildren<Text>().FirstOrDefault(x=>x.name=="StartBtnText_Text");
   if(status!=null)status.text=members.Length+" / "+flowPartySize+"   ·   "+(members.Length>=flowPartySize?"입장 준비 중":"참가자 기다리는 중");
   Canvas.ForceUpdateCanvases();
   if(flowAuto)StartCoroutine(CaptureMatchingRoster(members.Length));
  }

  void DrawMatchingParty() {
   if(flowParty==null||matchingSlots==null)return;
   // Convert the actual prefab slot anchor through the same viewport transform
   // used by the avatar renderer. Never approximate it with fixed screen pixels.
   float scale=Mathf.Min(Screen.width/1280f,Screen.height/800f);
   var origin=new Vector2((Screen.width-1280*scale)*.5f,(Screen.height-800*scale)*.5f);
   float canvasScale=flowParty.GetComponent<Canvas>().scaleFactor;
   var idle=art["player"].clips.First(c=>c.name=="idle");
   int frame=RaidMotion.Frame(idle.frames,idle.fps,idle.durations,Time.unscaledTime,false);
   for(int i=0;i<matchingMembers.Length&&i<matchingSlots.Length;i++) {
    var slot=matchingSlots[i];var world=slot.TransformPoint(new Vector3(slot.rect.center.x,slot.rect.center.y,0));
    var screen=RectTransformUtility.WorldToScreenPoint(null,world);
    var foot=(new Vector2(screen.x,Screen.height-screen.y)-origin)/scale;
    string look=matchingMembers[i].Appearance;
    if(string.IsNullOrEmpty(look))look=flowPeople.Values.FirstOrDefault(p=>p.Name==matchingMembers[i].Name)?.Appearance??bk.defaultLook;
    DrawAvatar(look,frame,foot,2.25f*canvasScale/scale,1,Color.white,false,true);
   }
  }
  IEnumerator CaptureMatchingRoster(int count) {
   yield return new WaitForEndOfFrame();
   if(flowStage=="Matching"&&!flowBusy){Capture("matching-"+count+".png");FlowRecord("MatchingRoster "+string.Join("|",matchingMembers.Select(m=>m.Name+":"+m.Appearance)));}
  }
  bool StartMatchingPreview(string[] args) {
   if(Array.IndexOf(args,"--matching-layout-review")<0)return false;
   matchingPreview=true;flowMode=true;flowStage="Matching";
   UpdateFlowParty(Enumerable.Range(0,4).Select(i=>{var look=ParseLook(localLook);foreach(var part in bk.parts)if(new[]{"hair","coat","pants","shoes","cap"}.Contains(part.part))look[part.part]=part.options[part.options.Length-1-i%3].code;return new LobbyPlayerInfo{Name=new[]{"새벽의 버서커","붉은달","카잔의 후예","폭룡왕 도전자"}[i],Appearance=JoinLook(look)};}).ToArray());
   StartCoroutine(CaptureMatchingLayouts());return true;
  }
  IEnumerator CaptureMatchingLayouts() {
   Directory.CreateDirectory(smokeDirectory);
   foreach(var size in new[]{new Vector2Int(1280,800),new Vector2Int(1280,720),new Vector2Int(1024,768),new Vector2Int(1600,686)}) {
    Screen.SetResolution(size.x,size.y,false);yield return new WaitForSecondsRealtime(1.2f);yield return new WaitForEndOfFrame();
    Capture("matching-"+Screen.width+"x"+Screen.height+".png");
    FlowRecord("MatchingViewport "+Screen.width+"x"+Screen.height+" slots="+matchingSlots.Length);
   }
   File.WriteAllLines(Path.Combine(smokeDirectory,"layout-audit.txt"),flowAudit);
   File.WriteAllText(Path.Combine(smokeDirectory,"FIXTURE.txt"),"Actual Unity matching prefab, four sample appearances, live window resize. UI-only fixture; not network matching evidence.");
   yield return new WaitForSecondsRealtime(.5f);Application.Quit(errors==0?0:1);
  }
 }
}
