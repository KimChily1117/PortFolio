using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Google.Protobuf;
using Google.Protobuf.Protocol;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
namespace DnfRaidDemo {
 public sealed partial class RaidDemoApp {
  readonly HashSet<string> flowCutSeen=new HashSet<string>();
  bool flowWardrobe;
  bool flowAppearancePending,flowStyleApplied;
  string flowRequestedStyle,flowLoadedLook;
  string flowFirstRaidLook,flowReturnedLook;
  bool flowMode,flowAuto,flowBusy,flowPublic,flowEquipped,flowReturned,flowAuditWritten,flowReturnSent;
  string flowInvite="";
  bool flowFriendMode;
  string flowFriendError;
  string flowStage="Login",flowAccount="",flowCharacter="",flowStatus="계정을 입력해 접속하세요.",flowTarget="";
  float flowAt,flowProgress,flowFade,flowNextMove,flowRaidAt,flowReturnAfter=65;
  ObjectInfo flowMe;
  readonly Dictionary<int,ObjectInfo> flowPeople=new Dictionary<int,ObjectInfo>();
  readonly List<ItemInfo> flowItems=new List<ItemInfo>();
  readonly List<LobbyPlayerInfo> flowCharacters=new List<LobbyPlayerInfo>();
  readonly List<string> flowAudit=new List<string>();
  GameObject flowParty;
  Camera flowCamera;
  AudioSource flowMusic;
  S_EquipmentStats flowEquipmentStats=new S_EquipmentStats{PhysicalAttack=100,MoveSpeedScale=1,AttackSpeedScale=1};
  [Serializable] class EquipmentDef {public int id;public string name,iconPath;public int damage,defence;public float moveSpeedPercent,attackSpeedPercent;}
  [Serializable] class EquipmentCatalog {public EquipmentDef[] weapons,armors;}
  EquipmentCatalog flowCatalog;
  int flowPartySize=4;
  bool flowReentryAuto;int flowRound;float flowReviveAt;
  string Arg(string name,string fallback="") {var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,name);return i>=0&&i+1<a.Length?a[i+1]:fallback;}
  void FlowRecord(string name){flowAudit.Add(Time.realtimeSinceStartup.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+" "+name);Debug.Log("[DAWN_FLOW] "+name);}
  void FlowState(string value){flowStage=value;flowAt=Time.realtimeSinceStartup;FlowRecord(value);}
  void BeginFlow() {
   flowMode=true;flowAuto=Array.IndexOf(Environment.GetCommandLineArgs(),"--flow-auto")>=0;
   flowReentryAuto=flowAuto&&Array.IndexOf(Environment.GetCommandLineArgs(),"--reentry-auto")>=0;
   int selectedSize;if(int.TryParse(Arg("--party-size","4"),out selectedSize)&&selectedSize>=1&&selectedSize<=4)flowPartySize=selectedSize;
   flowAccount=Arg("--account",PlayerPrefs.GetString("DawnAccount",""));flowCharacter=Arg("--character",flowAccount);
   int variant;if(int.TryParse(Arg("--style-index"),out variant)) {
    var look=ParseLook(localLook);foreach(var part in bk.parts)if(new[]{"hair","coat","pants","shoes","cap","neck"}.Contains(part.part))look[part.part]=part.options[part.options.Length-1-(variant%3)].code;
    look["weapon"]=bk.weapons[bk.weapons.Length-1-(variant%9)].code;flowRequestedStyle=JoinLook(look);
   }
   string inviteFile=Arg("--invite-file","");if(!string.IsNullOrEmpty(inviteFile))flowInvite=File.ReadAllText(inviteFile).Trim();
   hostAddress=Arg("--server","127.0.0.1");portText=Arg("--port","28182");
   FlowLoadFriendProfile();
   FlowLoadMemberEndpoint();
   float.TryParse(Arg("--return-after","65"),out flowReturnAfter);
   dawnAuto=flowAuto;dawnVerifySeconds=0;
   flowCatalog=JsonUtility.FromJson<EquipmentCatalog>(Resources.Load<TextAsset>("DawnEquipment/catalog").text);
   sim=new RaidSimulation(4);flowMusic=gameObject.AddComponent<AudioSource>();flowMusic.loop=true;flowMusic.volume=.45f;
   StartCoroutine(FlowTransition("DawnLogin","Login",null));
  }
  void FlowLogin() {
   if(flowBusy || string.IsNullOrWhiteSpace(flowAccount))return;
   if(flowFriendMode && (flowFriendError!=null || !FlowFriendNickname()))return;
   if(flowMemberMode && (flowMemberError!=null || !FlowMemberInputs(false)))return;
   try {
    ResetChat();net?.Dispose();net=new RaidNetwork();net.StartDawn(hostAddress.Trim(),int.Parse(portText),false,flowAccount.Trim(),flowInvite.Trim(),flowMemberMode?flowPassword:null);
    if(flowMemberMode){flowPassword="";PlayerPrefs.SetString("DawnMemberAccount",flowAccount);}
    net.Dawn.FlowMessage+=FlowPacket;net.Dawn.PersonalFeedback+=e=>{if(AcceptFeedback(e))PlayOriginal(e);};
    flowCharacters.Clear();flowItems.Clear();flowPeople.Clear();flowMe=null;flowEquipped=false;flowReturned=false;flowReturnSent=false;
    flowStatus="계정 정보를 확인하고 있습니다.";FlowState("Connecting");
    net.Dawn.SendFlow(new C_Login{UniqueId=flowAccount.Trim()});PlayerPrefs.SetString("DawnAccount",flowAccount.Trim());
   }catch(Exception e){flowStatus="접속 실패: "+e.Message;FlowState("Login");net?.Dispose();net=null;}finally{if(flowMemberMode)flowPassword="";}
  }
  void FlowEnter(string name) {
   flowCharacter=name;flowAppearancePending=false;flowStyleApplied=false;flowLoadedLook=null;
   if(flowFriendMode){PlayerPrefs.SetString("DawnFriendName/"+flowAccount,name);PlayerPrefs.Save();}
   StartCoroutine(FlowTransition("DawnTown","Town",()=>net.Dawn.SendFlow(new C_EnterGame{Name=name})));
  }
  void FlowPacket(IMessage message) {
   if(ChatPacket(message))return;
   if(message is S_RaidResume resume){FlowRecord("Resume "+resume.Status);if(resume.Status!="Loading"){flowStatus="레이드 복귀 불가: "+resume.Status;FlowState("Characters");}}
   else if(message is S_RaidCinematic cinematic){HandleClearCinematic(cinematic);}
   else if(message is S_RaidRewards rewards){ShowRaidRewards(rewards);FlowRecord("Rewards "+rewards.Status+" "+rewards.Encounter);}
   else if(message is S_ChannelList list){ChannelListResult(list);}
   else if(message is S_ChannelMove channel){ChannelResult(channel);}
   else if(message is S_Login login){
    if(login.LoginOK!=1){flowStatus="로그인에 실패했습니다. 서버 연결과 계정을 확인하세요.";FlowState("Login");return;}
    flowCharacters.Clear();flowCharacters.AddRange(login.Players);FlowState("Characters");flowStatus="캐릭터를 선택하거나 새로 만드세요.";
    if(flowFriendMode){if(flowCharacters.Count>0)FlowEnter((flowCharacters.FirstOrDefault(c=>c.Name==flowCharacter)??flowCharacters[0]).Name);else FlowFriendCreate();}
   }else if(message is S_CreatePlayer create){
    if(create.Player==null){flowStatus="사용 중인 이름이거나 캐릭터 생성 한도(4개)를 넘었습니다.";if(flowFriendMode)FlowState("Characters");return;}
    flowCharacters.Add(create.Player);FlowRecord("CharacterCreated "+create.Player.PlayerDbId);FlowEnter(create.Player.Name);
   }else if(message is S_EquipmentStats stats){flowEquipmentStats=stats.Clone();}
   else if(message is S_ItemList items){flowItems.Clear();flowItems.AddRange(items.Items.Select(i=>i.Clone()));FlowRecord("InventoryLoaded "+flowItems.Count);}
   else if(message is S_EquipItem equip){var item=flowItems.FirstOrDefault(i=>i.ItemDbId==equip.ItemDbId);if(item!=null)item.Equipped=equip.Equipped;FlowRecord("EquipmentConfirmed "+equip.ItemDbId+" "+equip.Equipped);}
   else if(message is S_EnterGame enter){
    flowMe=enter.Player.Clone();if(flowReturned)flowReturnedLook=flowMe.Appearance;
    if(flowStage=="Town") {
     if(flowLoadedLook==null){flowLoadedLook=flowMe.Appearance;FlowRecord("SqlAppearanceLoaded "+flowLoadedLook);}
     if(!string.IsNullOrEmpty(flowMe.Appearance))FlowAcceptAppearance(flowMe.Appearance);
     else {localLook=PlayerPrefs.GetString("DawnLook/"+flowCharacter,bk.defaultLook??"");FlowSendAppearance(true);}
     if(flowAuto && !flowStyleApplied && !flowAppearancePending && !string.IsNullOrEmpty(flowRequestedStyle)) {
      flowStyleApplied=true;localLook=flowRequestedStyle;FlowSendAppearance();
     }
    }
    flowPeople[flowMe.ObjectId]=flowMe;if(flowStage=="Town")FlowRecord("TownAppearance "+flowMe.Appearance);flowPublic=flowMe.PosInfo.PosX>=18;
    FlowRecord("Entered "+flowStage+" object="+flowMe.ObjectId);FlowTownCamera();
    if(flowStage=="Town")flowStatus=flowReturned?"레이드를 마치고 마을로 돌아왔습니다.":"세리아방에 도착했습니다.";
   }else if(message is S_Spawn spawn){foreach(var p in spawn.Objects)flowPeople[p.ObjectId]=p.Clone();}
   else if(message is S_Despawn despawn){foreach(int id in despawn.PlayerIds)if(flowMe==null||id!=flowMe.ObjectId)flowPeople.Remove(id);}
   else if(message is S_Appearance appearance){
    if(flowPeople.TryGetValue(appearance.PlayerId,out var shown))shown.Appearance=appearance.Look;
    if(flowMe!=null && appearance.PlayerId==flowMe.ObjectId) {
     flowAppearancePending=false;flowMe.Appearance=appearance.Look;FlowAcceptAppearance(appearance.Look);
     flowStatus=string.IsNullOrEmpty(appearance.Error)?"코디가 서버에 저장되었습니다.":"코디 저장 실패 · 기존 외형을 유지합니다. 다시 선택해 주세요.";
    }
    FlowRecord((string.IsNullOrEmpty(appearance.Error)?"AppearanceConfirmed ":"AppearanceRejected "+appearance.Error+" ")+appearance.PlayerId+" "+appearance.Look);
   }
   else if(message is S_Move movement){if(flowPeople.TryGetValue(movement.PlayerId,out var p))p.PosInfo=movement.PosInfo.Clone();}
   else if(message is S_CreateRoom room){if(room.ResponseCode!=1){flowStatus="매칭 신청을 완료하지 못했습니다. 장비와 입장 인원을 확인하세요.";FlowState("Town");if(flowParty!=null)Destroy(flowParty);}else if(room.PartySize>=1&&room.PartySize<=4)flowPartySize=room.PartySize;}
   else if(message is S_EnterParty party){flowStatus="레이드 참가자 "+party.PartyMembers.Count+" / "+flowPartySize;UpdateFlowParty(party.PartyMembers.ToArray());if(party.ResponseCode==1&&party.PartyMembers.Count>=flowPartySize)BeginMatchingCountdown();}
   else if(message is S_SceneMove move){
    if(move.TargetRoomType!=RoomType.Town&&flowStage=="Matching"&&MatchingCountdownActive){
     if(matchingSceneMove==null){matchingSceneMove=move.Clone();StartCoroutine(WaitMatchingCountdown());}
     return;
    }
    if(flowBusy){StartCoroutine(FlowDeferredPacket(message));return;}bool town=move.TargetRoomType==RoomType.Town;
    if(town){ResetClearCinematic();HideRaidRewards();flowReturned=true;net.Dawn.WriteAudit(smokeDirectory);FlowRecord("ReturnReason "+net.Dawn.EndReason);
     if(flowReentryAuto){string round=Path.Combine(smokeDirectory,"round-"+flowRound);Directory.CreateDirectory(round);net.Dawn.WriteAudit(round);File.WriteAllLines(Path.Combine(round,"flow-audit.txt"),flowAudit);}}
    flowPeople.Clear();flowMe=null;inventoryWindow=skillWindow=pauseMenu=false;
    StartCoroutine(FlowTransition(town?"DawnTown":"Demo",town?"Town":"RaidLoading",()=>{
     // Do blocking asset/decode work before the all-ready server barrier.
     // Otherwise the shared countdown can pass its first digit while loading.
     if(!town){PrepareClearCinematic();PreloadCutscenes();PrepareDawnHud();}
     net.Dawn.SendFlow(new C_SceneReady{TargetRoomId=move.TargetRoomId,TransferId=move.TransferId,TargetRoomType=move.TargetRoomType,SceneType=move.SceneType});
     FlowRecord("SceneReady "+move.TargetRoomType+" transfer="+move.TransferId);
    }));
   }else if(message is S_RaidStart raidStart){
    if(clearVideo==null)PrepareClearCinematic();raidEntryStage=-1;flowRound++;FlowRecord("EncounterStart round="+flowRound+" encounter="+raidStart.Encounter);flowReviveAt=0;
    flowReturned=flowReturnSent=flowAuditWritten=false;flowCutSeen.Clear();
    var equipment=ProjectDawn.Combat.RaidSnapshotCodec.Decode(raidStart.Content.ToByteArray());FlowRecord("ServerLoadout "+string.Join(",",equipment.Players.Select(p=>p.PhysicalAttack.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+"/"+p.EquipmentDefence)));
    flowFirstRaidLook=equipment.Players[raidStart.PlayerSlots.IndexOf(raidStart.Self)].Look;FlowRecord("RaidAppearance "+flowFirstRaidLook);
    inGame=true;PreloadCutscenes();PrepareDawnHud();playedEvents.Clear();dawnFeedback.Clear();ResetHitFeedback();displayPositions.Clear();displayHeights.Clear();animations.Clear();
    accumulator=0;dash=jump=false;raidRevivePressed=false;pressedSkills=0;dawnStartAt=-1;flowRaidAt=Time.realtimeSinceStartup;FlowState("Raid");
   }
  }
  IEnumerator FlowDeferredPacket(IMessage message){while(flowBusy)yield return null;FlowPacket(message);}
  S_SceneMove matchingSceneMove;
  IEnumerator WaitMatchingCountdown(){
   while(flowStage=="Matching"&&MatchingCountdownActive)yield return null;
   var move=matchingSceneMove;matchingSceneMove=null;
   if(move!=null&&flowStage=="Matching"){FlowRecord("MatchingCountdownEnd");FlowPacket(move);}
  }
  IEnumerator FlowTransition(string scene,string state,Action ready) {
   flowBusy=true;flowTarget=state;flowProgress=0;FlowRecord("TransitionBegin "+scene);flowStage="Loading";
   inGame=false;UpdateBakalMusic();ResetHitFeedback();if(dawnHud!=null){Destroy(dawnHud.gameObject);dawnHud=null;}if(flowParty!=null)Destroy(flowParty);
   float t=0;while(t<.35f){t+=Time.unscaledDeltaTime;flowFade=Mathf.Clamp01(t/.35f);yield return null;}
   GetComponentInChildren<Camera>().enabled=false;
   yield return SceneManager.LoadSceneAsync("DawnLoading");flowFade=0;
   float begin=Time.realtimeSinceStartup;var op=SceneManager.LoadSceneAsync(scene);op.allowSceneActivation=false;
   while(op.progress<.9f || Time.realtimeSinceStartup-begin<1.2f){flowProgress=Mathf.Clamp01(op.progress/.9f);yield return null;}
   FlowRecord("LoadingShown "+scene);Capture("loading-"+state+".png");yield return new WaitForEndOfFrame();
   op.allowSceneActivation=true;while(!op.isDone)yield return null;
   flowCamera=Camera.allCameras.FirstOrDefault(c=>c.transform.parent!=transform);
   if(scene=="Demo")GetComponentInChildren<Camera>().enabled=true;
   // The original title's Enter shortcut is replaced by real account/character selection.
   if(scene=="DawnLogin")foreach(var text in FindObjectsOfType<Text>())if(text.text.Contains("Enter")||text.text.Contains("ENTER")||text.text.Contains("Press Any"))text.enabled=false;
   FlowTownCamera();FlowMusic(state);
   if(state=="Town") {ApplyOriginalTownArt();dawnHud=new GameObject("Project Dawn original town HUD").AddComponent<DawnHudView>();dawnHud.Initialize(()=>ToggleOriginalInventory(false),()=>{skillWindow=!skillWindow;inventoryWindow=false;},null,null);}
   flowProgress=1;ready?.Invoke();FlowState(state);
   t=0;flowFade=1;while(t<.35f){t+=Time.unscaledDeltaTime;flowFade=1-Mathf.Clamp01(t/.35f);yield return null;}
   flowBusy=false;Capture("scene-"+state+".png");
  }
  void FlowMusic(string state) {
   string name=state=="Login"?"CharacterSelect":state=="Town"?"seria_gate":null;
   flowMusic.Stop();if(name==null)return;flowMusic.clip=Resources.Load<AudioClip>("DawnOriginal/FlowSounds/"+name);if(flowMusic.clip)flowMusic.Play();
  }
  void FlowTownCamera() {
   if(flowCamera==null || SceneManager.GetActiveScene().name!="DawnTown")return;
   flowCamera.orthographic=true;flowCamera.backgroundColor=Color.black;flowCamera.orthographicSize=3.6f;
   flowCamera.transform.position=new Vector3(flowPublic?25.79f:0,1.1f,-10);
  }
  void FlowGate(bool lobby) {if(flowBusy||flowAppearancePending||flowMe==null)return;StartCoroutine(FlowGateFade(lobby));}
  IEnumerator FlowGateFade(bool lobby) {
   flowBusy=true;inventoryWindow=skillWindow=false;
   float t=0;while(t<.25f){t+=Time.unscaledDeltaTime;flowFade=Mathf.Clamp01(t/.25f);yield return null;}
   FlowRecord(lobby?"GateToLobby":"GateToRoom");flowPeople.Clear();flowMe=null;
   net.Dawn.SendFlow(new C_TownGate{PublicArea=lobby});
   float wait=Time.realtimeSinceStartup;while(flowMe==null && Time.realtimeSinceStartup-wait<5)yield return null;
   if(flowMe==null){flowStatus="마을 이동 응답이 지연되고 있습니다.";}else {FlowTownCamera();flowStatus=lobby?"던전 입구 · 1~4인 레이드 매칭":"세리아방";}
   flowMusic.Stop();flowMusic.clip=Resources.Load<AudioClip>("DawnOriginal/FlowSounds/"+(lobby?"bakal_ready":"seria_gate"));if(flowMusic.clip)flowMusic.Play();
   t=0;while(t<.25f){t+=Time.unscaledDeltaTime;flowFade=1-Mathf.Clamp01(t/.25f);yield return null;}
   flowBusy=false;Capture("town-gate.png");
  }
  void FlowMatch() {if(flowBusy||channelOpen||flowAppearancePending||flowMe==null||!flowPublic||flowStage!="Town")return;PreloadRewardMusic();PreloadCountdownArt();matchingCountdownAt=-1;matchingSceneMove=null;raidEntryStage=-1;FlowState("Matching");flowStatus=flowPartySize+"인 매칭 대기 · 같은 인원을 선택한 참가자가 모이면 입장합니다.";UpdateFlowParty(new[]{new LobbyPlayerInfo{Name=flowMe.Name,Appearance=flowMe.Appearance}});net.Dawn.SendFlow(new C_CreateRoom{PartySize=flowPartySize});FlowRecord("MatchSize "+flowPartySize);}
  bool UpdateFlow() {
   UpdatePortfolioChat();
   if(net!=null && flowStage!="Raid")net.Poll(sim);
   if(net?.Dawn.EndReason=="TransportFailure" && flowStage!="Disconnected"){StopAllCoroutines();flowBusy=false;inGame=false;if(flowParty)Destroy(flowParty);FlowState("Disconnected");flowStatus="연결이 끊겼습니다. 같은 계정·캐릭터로 3분 안에 접속하면 전투에 복귀합니다. 방장 이탈 시에는 전투가 종료됩니다.";}
   if(flowStage=="Raid"){
    if(flowAuto && Time.realtimeSinceStartup-flowRaidAt>2 && Time.realtimeSinceStartup-flowRaidAt<2.1f)Capture("raid.png");
    if(flowAuto && !portfolioDemo && !(flowReentryAuto&&flowRound==1) && !flowReturnSent && Time.realtimeSinceStartup-flowRaidAt>flowReturnAfter && net.Dawn.Client.IsHost){flowReturnSent=true;net.Dawn.SendFlow(new C_RaidLeave{Encounter=net.Dawn.Client.Start.Encounter});FlowRecord("PartyReturnRequested");}
    return false;
   }
   if(Input.GetKeyDown(KeyCode.F12))Capture("flow-manual.png");
   if(!flowBusy && flowStage=="Town" && !ChatBlocksInput && !CommandsBlockGameplay) {
    if(Input.GetKeyDown(KeyCode.I))ToggleOriginalInventory(false);
    if(Input.GetKeyDown(KeyCode.U))ToggleOriginalInventory(true);
    if(Input.GetKeyDown(KeyCode.K)){skillWindow=!skillWindow;inventoryWindow=false;}
    if(!inventoryWindow && !skillWindow && !channelOpen && flowMe!=null && Time.unscaledTime>=flowNextMove){
     var delta=new Vector2((Input.GetKey(KeyCode.RightArrow)?1:0)-(Input.GetKey(KeyCode.LeftArrow)?1:0),(Input.GetKey(KeyCode.UpArrow)?1:0)-(Input.GetKey(KeyCode.DownArrow)?1:0));
     if(delta.sqrMagnitude>1)delta.Normalize();
     var p=flowMe.PosInfo;if(delta.sqrMagnitude>0){p.PosX=Mathf.Clamp(p.PosX+delta.x*.18f*flowEquipmentStats.MoveSpeedScale,flowPublic?18:-5,flowPublic?33:5);p.PosY=Mathf.Clamp(p.PosY+delta.y*.12f*flowEquipmentStats.MoveSpeedScale,-1.5f,1.2f);p.State=Google.Protobuf.Protocol.PlayerState.Moving;p.MoveDir=delta.x<0?MoveDir.Left:MoveDir.Right;}
     else p.State=Google.Protobuf.Protocol.PlayerState.Idle;
     net.Dawn.SendFlow(new C_Move{PosInfo=p.Clone()});flowNextMove=Time.unscaledTime+.05f;
    }
   }
   if(dawnHud!=null)dawnHud.Display(new PlayerState{Hp=100,MaxHp=100,Mp=100,MaxMp=100},!RaidRewardsVisible&&(flowStage=="Town"||flowStage=="Matching"));
   if(flowAuto && !flowBusy){float age=Time.realtimeSinceStartup-flowAt;
    if(flowStage=="Login" && age>1)FlowLogin();
    else if(flowStage=="Characters" && age>1){if(flowCharacters.Count>0)FlowEnter(flowCharacters[0].Name);else{net.Dawn.SendFlow(new C_CreatePlayer{Name=flowCharacter});flowAt=Time.realtimeSinceStartup+10;}}
    else if(flowStage=="Town" && flowMe!=null){
     if(portfolioDemo && flowReturned){PortfolioReturned();}
     else if(flowReturned && age>3 && !flowAuditWritten){FlowRecord("RoundTripComplete");WriteFlowAudit();Capture("returned-town.png");flowAuditWritten=true;}
     else if(flowReturned && age>5 && flowAuditWritten){
      if(flowReentryAuto&&flowRound==1){flowReturned=flowAuditWritten=flowReturnSent=false;FlowRecord("ReentryRequested");flowAt=Time.realtimeSinceStartup;}
      else Application.Quit(errors==0?0:1);}
     else if(!flowReturned && !flowEquipped && age>2){var item=flowItems.FirstOrDefault(i=>i.TemplateId==91002);if(item!=null)net.Dawn.SendFlow(new C_EquipItem{ItemDbId=item.ItemDbId,Equipped=true});flowEquipped=true;inventoryWindow=true;Capture("inventory.png");}
     else if(!flowReturned && !flowPublic && age>4)FlowGate(true);
     else if(!flowReturned && flowPublic && (portfolioDemo?PortfolioAge>60+(portfolioSlot-1)*3:age>7))FlowMatch();
    }
   }
   if(flowAuto && Time.realtimeSinceStartup-startedAt>(portfolioDemo?900:flowReentryAuto?420:180) && !flowAuditWritten){FlowRecord("Timeout");WriteFlowAudit();Application.Quit(2);}
   return true;
  }
  void WriteFlowAudit(){Directory.CreateDirectory(smokeDirectory);File.WriteAllLines(Path.Combine(smokeDirectory,"flow-audit.txt"),flowAudit);net?.Dawn.WriteAudit(smokeDirectory);File.WriteAllText(Path.Combine(smokeDirectory,"render-result.json"),JsonUtility.ToJson(new DawnRenderResult{errors=errors,frames=frameCount,repaints=guiRepaints,width=Screen.width,height=Screen.height,localSlot=net.LocalPlayerId,players=sim.Players.Count,autoInput=flowAuto},true));File.WriteAllText(Path.Combine(smokeDirectory,"flow-result.json"),JsonUtility.ToJson(new FlowResult{completed=flowReturned&&flowMe!=null,errors=errors,stage=flowStage,items=flowItems.Count,equipped=flowItems.Where(x=>x.Equipped).Select(x=>x.TemplateId).ToArray(),account=flowAccount,character=flowCharacter,raidLook=flowFirstRaidLook,returnedLook=flowReturnedLook,loadedLook=flowLoadedLook},true));}
  [Serializable] class FlowResult {public bool completed;public int errors,items;public int[] equipped;public string stage,account,character,raidLook,returnedLook,loadedLook;}
  void FlowAcceptAppearance(string look) {
   localLook=string.IsNullOrEmpty(look)?bk.defaultLook??"":look;
   if(!string.IsNullOrEmpty(look)){PlayerPrefs.SetString("DawnLook/"+flowCharacter,look);PlayerPrefs.Save();}
  }
  void FlowSendAppearance(bool initializeOnly=false){
   if(flowStage!="Town" || flowAppearancePending || flowMe==null || net?.Dawn==null)return;
   flowAppearancePending=true;flowStatus="코디를 서버에 저장하고 있습니다…";
   net.Dawn.SendFlow(new C_Appearance{Look=localLook,InitializeOnly=initializeOnly});
   FlowRecord("AppearanceSaveRequested "+(initializeOnly?"Import":"Edit"));
  }
  bool DrawFlow() {
   if(flowStage=="Raid") return false;
   if(flowStage=="Loading"){
    Fill(new Rect(0,590,1280,210),new Color(.02f,.025f,.035f,.92f));Label(new Rect(280,620,720,48),flowTarget=="RaidLoading"?"매칭 완료 · 전장으로 이동합니다":flowTarget=="Town"?"마을로 이동합니다":"Project Dawn",27,Color.white,true,TextAnchor.MiddleCenter);
    Bar(new Rect(380,688,520,5),flowProgress,cyan);Label(new Rect(280,710,720,30),flowProgress<1?"목적지를 불러오는 중입니다…":"입장을 준비하고 있습니다…",17,Color.white,false,TextAnchor.MiddleCenter);
   } else if(flowStage=="Login"||flowStage=="Connecting"||flowStage=="Registering"||flowStage=="Creating"||flowStage=="Characters"||flowStage=="Disconnected") {
    var bg=Resources.Load<Texture2D>("DawnOriginal/FlowDependencies/c95d4737a68c56047936e6a7cd332dc0");if(bg)GUI.DrawTexture(new Rect(0,0,1280,800),bg,ScaleMode.ScaleAndCrop);
    OriginalChrome(new Rect(360,220,560,410),"",false);Label(new Rect(400,244,480,45),"PROJECT DAWN",32,gold,true,TextAnchor.MiddleCenter);
    Label(new Rect(400,300,480,30),flowStatus,16,Color.white,false,TextAnchor.MiddleCenter);
    if(flowMemberMode&&flowStage!="Characters"){DrawMemberLogin();}
    else if(flowFriendMode){DrawFriendLogin();}
    else if(flowStage=="Characters"){
     int i=0;foreach(var c in flowCharacters.Take(4)){if(GUI.Button(new Rect(420,345+i++*42,440,34),c.Name+" · 마을 입장"))FlowEnter(c.Name);}
     flowCharacter=GUI.TextField(new Rect(420,530,280,36),flowCharacter,40);if(GUI.Button(new Rect(710,530,150,36),"캐릭터 생성"))net.Dawn.SendFlow(new C_CreatePlayer{Name=flowCharacter});
    }else {Label(new Rect(420,352,100,30),"계정 ID",18,Color.white);flowAccount=GUI.TextField(new Rect(530,350,330,36),flowAccount,64);
     Label(new Rect(420,410,100,30),"서버",18,Color.white);hostAddress=GUI.TextField(new Rect(530,406,230,36),hostAddress,64);portText=GUI.TextField(new Rect(770,406,90,36),portText,5);
     Label(new Rect(420,452,100,28),"초대 키",16,Color.white);flowInvite=GUI.PasswordField(new Rect(530,450,330,30),flowInvite,'*',256);
     GUI.enabled=flowStage!="Connecting"&&!flowBusy;if(GUI.Button(new Rect(420,485,440,48),"접속하기"))FlowLogin();GUI.enabled=true;
     Label(new Rect(410,555,460,42),"로컬: 계정 ID · 외부: tls:// 서버와 초대 키",14,Color.gray,false,TextAnchor.MiddleCenter);
    }
   } else {
    DrawFlowPeople();
    DrawSkillHud(new PlayerState{Id=0,Hp=100,MaxHp=100,Mp=100,MaxMp=100});
    Fill(new Rect(0,0,1280,70),ink);Label(new Rect(30,12,650,28),flowPublic?"던전 입구  /  바칼 레이드":"세리아방",23,gold,true);
    Label(new Rect(30,43,800,22),flowStatus,14,Color.white);
    Label(new Rect(28,692,660,30),"방향키 이동   ·   I 장비   ·   "+(flowMe?.Name??"입장 중…"),17,Color.white);
    if(flowStage=="Town" && !flowBusy){if(GUI.Button(new Rect(760,18,145,34),"채널 선택"))OpenChannels();if(GUI.Button(new Rect(920,18,145,34),"장비 / 가방"))ToggleOriginalInventory(false);
     if(GUI.Button(new Rect(1080,18,170,34),flowPublic?"세리아방으로":"던전 입구로"))FlowGate(!flowPublic);
     if(flowPublic){
      OriginalChrome(new Rect(990,542,265,148),"",false);
      Label(new Rect(1000,550,245,25),"입장 인원 · 최대 4명",17,gold,true);
      for(int size=1;size<=4;size++) {
       var r=new Rect(1000+(size-1)*62,583,58,34);
       if(GUI.Button(r,size+"인"))flowPartySize=size;
       if(flowPartySize==size)Outline(r,cyan,2);
      }
      Label(new Rect(1000,620,245,20),"보스 체력·피해량 동일",12,Color.gray);
      if(GUI.Button(new Rect(1000,645,245,38),flowPartySize==1?"혼자 입장":"선택한 "+flowPartySize+"인으로 매칭"))FlowMatch();
     }}
    if(inventoryWindow){if(flowWardrobe)DrawInventoryWindow();else DrawFlowInventory();}if(skillWindow)DrawSkillWindow();DrawChannels();
   }
   if(flowFade>0)Fill(new Rect(0,0,1280,800),new Color(0,0,0,flowFade));return true;
  }
  void DrawFlowPeople() {
   if(flowCamera==null)return;float scale=Mathf.Min(Screen.width/1280f,Screen.height/800f);float ox=(Screen.width-1280*scale)*.5f,oy=(Screen.height-800*scale)*.5f;
   foreach(var p in flowPeople.Values){if(p.PosInfo==null || (!flowPublic && p.ObjectId!=flowMe?.ObjectId))continue;
    var world=flowCamera.WorldToScreenPoint(new Vector3(p.PosInfo.PosX,p.PosInfo.PosY,0));var point=new Vector2((world.x-ox)/scale,(Screen.height-world.y-oy)/scale);
    string clipName=p.PosInfo.State==Google.Protobuf.Protocol.PlayerState.Moving?"walk":"idle";
    var clip=art["player"].clips.FirstOrDefault(c=>c.name==clipName)??art["player"].clips.First(c=>c.name=="idle");
    int frame=RaidMotion.Frame(clip.frames,clip.fps,clip.durations,Time.unscaledTime,false);
    DrawAvatar(p.Appearance,frame,point,1.3f,p.PosInfo.MoveDir==MoveDir.Left?-1:1,Color.white);
    Label(new Rect(point.x-110,point.y-155,220,24),p.Name,14,p.ObjectId==flowMe?.ObjectId?cyan:Color.white,true,TextAnchor.MiddleCenter);
   }
  }
  void DrawFlowInventory() { DrawOriginalEquipmentWindow(); }
 }
}
