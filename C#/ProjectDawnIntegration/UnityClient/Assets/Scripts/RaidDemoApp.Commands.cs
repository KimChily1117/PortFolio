using System;
using System.Collections.Generic;
using System.Linq;
using ProjectDawn.Network;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DnfRaidDemo
{
    public sealed partial class RaidDemoApp
    {
        const string SkillCommandsPref="dawn_skill_commands_v1";
        [Serializable] sealed class SavedSkillCommand { public int skill; public int[] tokens; }
        [Serializable] sealed class SavedSkillCommands { public int version=1; public SavedSkillCommand[] bindings; }
        readonly SkillCommandRecognizer skillCommands=new SkillCommandRecognizer();
        readonly List<SkillCommandBinding> commandBindings=new List<SkillCommandBinding>();
        readonly List<SkillCommandToken> commandDraft=new List<SkillCommandToken>();
        bool commandWindow, commandSettingsLoaded;
        int commandSelected=(int)SkillId.Berserk, commandBlockedFrame=-1, commandEscapeFrame=-1;
        SkillCommandKeys commandConsumed;
        string commandNotice="",commandContext="";
        RaidSimulation commandSimulation;
        Vector2 commandScroll;
        bool CommandEscapeHandled { get { return commandEscapeFrame==Time.frameCount; } }
        bool CommandsBlockGameplay { get { return commandWindow || commandBlockedFrame==Time.frameCount || CommandTextFocused(); } }
        static readonly string[] CommandLabels={"앞 →","뒤 ←","위 ↑","아래 ↓","Z","X","Space"};
        static bool CommandTextFocused()
        {
            var selected=EventSystem.current!=null?EventSystem.current.currentSelectedGameObject:null;
            return Input.imeCompositionMode==IMECompositionMode.On || !string.IsNullOrEmpty(Input.compositionString)
                || (selected!=null && (selected.GetComponent<UnityEngine.UI.InputField>()!=null || selected.GetComponent<TMPro.TMP_InputField>()!=null));
        }
        static SkillCommandKeys CommandHeldKeys()
        {
            SkillCommandKeys keys=SkillCommandKeys.None;
            if(Input.GetKey(KeyCode.LeftArrow))keys|=SkillCommandKeys.Left;
            if(Input.GetKey(KeyCode.RightArrow))keys|=SkillCommandKeys.Right;
            if(Input.GetKey(KeyCode.UpArrow))keys|=SkillCommandKeys.Up;
            if(Input.GetKey(KeyCode.DownArrow))keys|=SkillCommandKeys.Down;
            if(Input.GetKey(KeyCode.Z))keys|=SkillCommandKeys.Z;
            if(Input.GetKey(KeyCode.X))keys|=SkillCommandKeys.X;
            if(Input.GetKey(KeyCode.Space))keys|=SkillCommandKeys.Space;
            return keys;
        }
        bool CommandKeyConsumed(KeyCode key)
        {
            var mask=key==KeyCode.X?SkillCommandKeys.X:key==KeyCode.Z?SkillCommandKeys.Z:key==KeyCode.Space?SkillCommandKeys.Space:SkillCommandKeys.None;
            return (commandConsumed&mask)!=0;
        }
        bool CommandableSkill(int id)
        { return id>0 && id<SkillBook.Count && SkillBook.All[id].Kind!=SkillKind.Basic && SkillBook.All[id].Kind!=SkillKind.Dodge; }
        void LoadSkillCommands()
        {
            if(commandSettingsLoaded)return;
            commandSettingsLoaded=true;
            try {
                string json=PlayerPrefs.GetString(SkillCommandsPref,"");
                if(json!="") {
                    var saved=JsonUtility.FromJson<SavedSkillCommands>(json);
                    if(saved==null || saved.version!=1 || saved.bindings==null)throw new FormatException();
                    foreach(var b in saved.bindings) {
                        if(b==null || b.tokens==null || !CommandableSkill(b.skill))throw new FormatException();
                        commandBindings.Add(new SkillCommandBinding(b.skill,b.tokens.Select(t=>(SkillCommandToken)t).ToArray()));
                    }
                    if(SkillCommandRecognizer.Validate(commandBindings)!=null)throw new FormatException();
                    LoadCommandDraft();return;
                }
            } catch(Exception) { commandNotice="저장된 커맨드를 읽을 수 없어 기본값을 사용합니다."; }
            DefaultSkillCommands(false);
        }
        void DefaultSkillCommands(bool save)
        {
            commandBindings.Clear();
            commandBindings.Add(new SkillCommandBinding((int)SkillId.Berserk,SkillCommandToken.Forward,SkillCommandToken.Forward,SkillCommandToken.Space));
            commandBindings.Add(new SkillCommandBinding((int)SkillId.Frenzy,SkillCommandToken.Down,SkillCommandToken.Down,SkillCommandToken.Space));
            LoadCommandDraft();if(save)SaveSkillCommands();
        }
        void SaveSkillCommands()
        {
            var saved=new SavedSkillCommands{bindings=commandBindings.Select(b=>new SavedSkillCommand{skill=b.Skill,tokens=b.Tokens.Select(t=>(int)t).ToArray()}).ToArray()};
            PlayerPrefs.SetString(SkillCommandsPref,JsonUtility.ToJson(saved));PlayerPrefs.Save();ResetSkillCommandInput();
        }
        void ResetSkillCommandInput()
        {
            var keys=CommandHeldKeys();skillCommands.Reset(keys);commandConsumed=keys;
            commandBlockedFrame=Time.frameCount;dash=jump=false;pressedSkills=0;runInput.Reset();
        }
        void LoadCommandDraft()
        {
            commandDraft.Clear();var binding=commandBindings.Find(b=>b.Skill==commandSelected);
            if(binding!=null)commandDraft.AddRange(binding.Tokens);
        }
        void OpenSkillCommands()
        {
            LoadSkillCommands();commandWindow=true;skillWindow=inventoryWindow=false;commandNotice="";LoadCommandDraft();
            GUI.FocusControl(null);ResetSkillCommandInput();
        }
        void CloseSkillCommands()
        { commandWindow=false;GUI.FocusControl(null);ResetSkillCommandInput(); }
        void UpdateSkillCommands()
        {
            LoadSkillCommands();
            var keys=CommandHeldKeys();commandConsumed&=keys;
            bool location=inGame && (!flowMode || flowStage=="Raid") || flowMode && flowStage=="Town";
            if(!location || flowBusy || RaidRewardsVisible) { if(commandWindow)CloseSkillCommands(); }
            if(location && !flowBusy && !ChatBlocksInput && !showHelp && !pauseMenu && !channelOpen && !RaidRewardsVisible && !CommandTextFocused() && Input.GetKeyDown(KeyCode.F6)) {
                if(commandWindow)CloseSkillCommands();else OpenSkillCommands();
            }
            if(commandWindow && Input.GetKeyDown(KeyCode.Escape)) { commandEscapeFrame=Time.frameCount;CloseSkillCommands(); }
            PlayerState player=sim!=null && net!=null && net.LocalPlayerId>=0 && net.LocalPlayerId<sim.Players.Count?sim.Players[net.LocalPlayerId]:null;
            string context=(flowMode?flowStage:"Local")+":"+(inGame?"1":"0")+":"+(player!=null?player.RoomId.ToString():"-")+":"+(sim!=null && sim.CinematicActive?"Intro":"Play");
            if(commandSimulation!=sim || commandContext!=context) { commandSimulation=sim;commandContext=context;ResetSkillCommandInput(); }
            bool enabled=inGame && player!=null && player.Hp>0 && sim.Mode==EncounterMode.Fighting && !sim.CinematicActive
                && (!flowMode || flowStage=="Raid" && !flowBusy) && !ChatBlocksInput && !pauseMenu && !showHelp && !AnyWindow
                && !channelOpen && !RaidRewardsVisible && !CommandsBlockGameplay && Application.isFocused;
            if(!enabled) { pressedSkills=0;dash=jump=false;raidRevivePressed=potionPressed=false; }
            var matches=skillCommands.Sample(keys,player!=null?player.Facing:1,Time.unscaledTime,commandBindings,enabled);
            ApplySkillCommandMatches(matches);
        }
        void ApplySkillCommandMatches(IEnumerable<SkillCommandMatch> matches)
        {
            foreach(var match in matches) {
                pressedSkills|=PlayerCommand.Bit((SkillId)match.Skill);commandConsumed|=match.Terminal;
            }
        }
        string CommandText(IEnumerable<SkillCommandToken> tokens)
        { return string.Join("  ",tokens.Select(t=>CommandLabels[(int)t])); }
        void DrawSkillCommands()
        {
            if(!commandWindow)return;
            GUI.enabled=true;
            Fill(new Rect(0,0,1280,800),new Color(0,0,0,.65f));
            var window=new Rect(120,70,1040,660);
            Fill(window,new Color(.055f,.04f,.045f,.99f));
            if(OriginalChrome(window,"스킬 커맨드 설정 · F6")){CloseSkillCommands();return;}
            GUI.Label(new Rect(145,116,990,48),"앞/뒤는 입력 시작 시 캐릭터가 바라보는 방향 기준입니다. 같은 방향은 떼었다가 다시 누르세요.\n키 사이 0.45초 · 전체 1.4초 이내 · 기존 스킬 단축키도 그대로 사용합니다.",Style(16,Color.white,false,TextAnchor.UpperLeft));
            var skills=SkillBook.All.Where(s=>CommandableSkill((int)s.Id)).ToArray();
            commandScroll=GUI.BeginScrollView(new Rect(145,178,290,415),commandScroll,new Rect(0,0,264,skills.Length*43));
            for(int i=0;i<skills.Length;i++) {
                var skill=skills[i];var bound=commandBindings.Exists(b=>b.Skill==(int)skill.Id);
                var old=GUI.color;GUI.color=(int)skill.Id==commandSelected?gold:Color.white;
                if(GUI.Button(new Rect(0,i*43,260,38),skill.Name+(bound?"  ●":""))) { commandSelected=(int)skill.Id;LoadCommandDraft();commandNotice=""; }
                GUI.color=old;
            }
            GUI.EndScrollView();
            var chosen=SkillBook.All[commandSelected];
            GUI.Label(new Rect(460,180,650,36),chosen.Name,Style(25,gold,true,TextAnchor.MiddleLeft));
            GUI.Label(new Rect(460,224,650,68),commandDraft.Count==0?"미지정 · 아래 키를 순서대로 클릭하세요.":CommandText(commandDraft),Style(20,Color.white,true,TextAnchor.UpperLeft,true));
            for(int i=0;i<CommandLabels.Length;i++) {
                GUI.enabled=commandDraft.Count<SkillCommandRecognizer.MaximumLength;
                if(GUI.Button(new Rect(460+(i%4)*162,306+(i/4)*48,152,39),CommandLabels[i])) {commandDraft.Add((SkillCommandToken)i);commandNotice="";}
            }
            GUI.enabled=true;
            if(GUI.Button(new Rect(460,410,180,38),"마지막 키 삭제") && commandDraft.Count>0)commandDraft.RemoveAt(commandDraft.Count-1);
            if(GUI.Button(new Rect(655,410,180,38),"비우기"))commandDraft.Clear();
            if(GUI.Button(new Rect(850,410,234,38),"이 스킬 커맨드 해제")) { commandBindings.RemoveAll(b=>b.Skill==commandSelected);SaveSkillCommands();LoadCommandDraft();commandNotice="커맨드를 해제했습니다. 기존 단축키는 유지됩니다."; }
            if(GUI.Button(new Rect(460,462,250,44),"현재 조합 저장")) {
                var proposed=commandBindings.Where(b=>b.Skill!=commandSelected).ToList();proposed.Add(new SkillCommandBinding(commandSelected,commandDraft.ToArray()));
                string error=SkillCommandRecognizer.Validate(proposed);
                if(error!=null)commandNotice=error;
                else {commandBindings.Clear();commandBindings.AddRange(proposed);SaveSkillCommands();commandNotice="이 PC에 저장했습니다.";}
            }
            if(GUI.Button(new Rect(730,462,354,44),"전체 기본값 복원")) {DefaultSkillCommands(true);commandNotice="폭주: 앞 앞 Space / 프렌지: 아래 아래 Space";}
            GUI.Label(new Rect(460,520,635,80),commandNotice,Style(17,gold,false,TextAnchor.UpperLeft,true));
            string state="프렌지: 아래 아래 Space로 켜기/끄기 · OFF에서는 기본 한 손 무기 모션";
            if(sim!=null && net!=null && net.LocalPlayerId<sim.Players.Count)state="현재 프렌지 "+(sim.Players[net.LocalPlayerId].FrenzyTime>0?"ON · 2도류":"OFF · 기본 무기")+"  |  다시 사용하면 전환됩니다.";
            GUI.Label(new Rect(145,610,990,30),state,Style(17,gold,false,TextAnchor.MiddleLeft));
            GUI.Label(new Rect(145,648,800,45),"Space는 커맨드 전용 · Tab은 긴급회피입니다. 완성된 Z/X 커맨드는 평타로 중복 실행되지 않습니다.\n명중·버프 적용은 기존 전투 서버의 검증 결과를 따릅니다.",Style(14,Color.white,false,TextAnchor.UpperLeft));
            if(GUI.Button(new Rect(970,663,164,38),"닫기 · Esc"))CloseSkillCommands();
        }
    }
}
