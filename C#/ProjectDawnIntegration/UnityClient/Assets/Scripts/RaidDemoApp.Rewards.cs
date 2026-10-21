using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Google.Protobuf.Protocol;
using UnityEngine;
using ProjectDawn.Combat;

namespace DnfRaidDemo
{
    public sealed partial class RaidDemoApp
    {
        S_RaidRewards raidRewards;
        float raidRewardsAt, raidRewardsRevealAt = -1;
        bool raidRewardsVisible, rewardUiPreview;
        AudioSource rewardMusic;
        AudioClip rewardMusicClip;
        readonly Dictionary<string,AudioClip> rewardSfx=new Dictionary<string,AudioClip>();
        readonly HashSet<string> rewardSoundEvents=new HashSet<string>();
        AudioSource rewardSfxVoice,rewardCountVoice;
        readonly HashSet<int> rewardRevealed=new HashSet<int>();
        float RewardAge=>Time.realtimeSinceStartup-raidRewardsAt;
        float RewardFinishedAt=>raidRewardsRevealAt+RaidRewardPresentation.CardMs/1000f*(raidRewards?.Entries.Count??0);
        readonly Dictionary<string, Texture2D> rewardTextures = new Dictionary<string, Texture2D>();
        public bool RaidRewardsVisible { get { return raidRewardsVisible; } }

        // A receipt is presentation data. Opening a card never grants an item.
        void ShowRaidRewards(S_RaidRewards packet)
        {
            if (packet == null) return;
            if(packet.Status!="History" && ClearCinematicActive && packet.Encounter==clearCinema.Encounter){
                if(deferredClearRewards==null||deferredClearRewards.Status=="Pending"||packet.Status!="Pending")deferredClearRewards=packet.Clone();
                return;
            }
            bool same = raidRewards != null && raidRewards.Encounter == packet.Encounter && raidRewardsVisible;
            bool wasSaved = same && raidRewards.Status != "Pending";
            if (wasSaved && packet.Status == "Pending") return;
            raidRewards = packet.Clone();
            raidRewardsVisible = true;
            if (!same) {
                raidRewardsAt = Time.realtimeSinceStartup; raidRewardsRevealAt = -1;rewardRevealed.Clear();rewardSoundEvents.Clear();
                if(packet.Status!="History")StartRewardMusic();
                FlowRecord("ResultPresentationStart encounter="+packet.Encounter);
            }
            if (packet.Status != "Pending" && !wasSaved)
            {
                raidRewardsRevealAt = packet.Status == "History" ? Time.realtimeSinceStartup-10f : Math.Max(raidRewardsAt+RaidRewardPresentation.LeadMs/1000f,Time.realtimeSinceStartup);
                if(flowAuto && packet.Status=="Saved")StartCoroutine(CaptureSavedRaidReward());
            }
            inventoryWindow = skillWindow = pauseMenu = showHelp = false;
            if (dawnHud != null) { dawnHud.Display(null, false); dawnHud.DisplayBoss(null, false); }
        }

        void HideRaidRewards() { raidRewardsVisible = false;if(rewardMusic!=null)rewardMusic.Stop();if(rewardSfxVoice!=null)rewardSfxVoice.Stop();if(rewardCountVoice!=null)rewardCountVoice.Stop(); }
        void StartRewardMusic() {
            if(bgm!=null)bgm.Stop();if(flowMusic!=null)flowMusic.Stop();
            if(rewardMusic==null){rewardMusic=gameObject.AddComponent<AudioSource>();rewardMusic.playOnAwake=false;rewardMusic.loop=true;rewardMusic.spatialBlend=0;}
            rewardMusic.clip=rewardMusicClip;rewardMusic.volume=0;
            if(rewardMusic.clip!=null){rewardMusic.Play();FlowRecord("ResultBgm Liberation");}
        }
        void PreloadRewardMusic(){
            if(rewardMusicClip==null)rewardMusicClip=Resources.Load<AudioClip>("DawnOriginal/FlowSounds/bakal_result");
            // The imported clip disables preloadAudioData. Decode before combat,
            // not on Play at result entry (which otherwise stalls ~0.4 seconds).
            if(rewardMusicClip!=null)rewardMusicClip.LoadAudioData();
            foreach(var id in new[]{"bk_ui_count","bk_ui_card_unlock","bk_ui_card_open","bk_ui_card_appear"})if(!rewardSfx.ContainsKey(id))rewardSfx[id]=Resources.Load<AudioClip>("DawnRewards/Sounds/"+id);
        }
        void RewardSound(string key,string id,float volume,bool count=false) {
            if(!rewardSoundEvents.Add(key)||raidRewards.Status=="History")return;
            AudioClip clip;if(!rewardSfx.TryGetValue(id,out clip)||clip==null)return;
            if(count){
                if(rewardCountVoice==null){rewardCountVoice=gameObject.AddComponent<AudioSource>();rewardCountVoice.playOnAwake=false;}
                rewardCountVoice.Stop();rewardCountVoice.clip=clip;rewardCountVoice.volume=volume;rewardCountVoice.Play();
            }else{
                if(rewardSfxVoice==null){rewardSfxVoice=gameObject.AddComponent<AudioSource>();rewardSfxVoice.playOnAwake=false;}
                rewardSfxVoice.PlayOneShot(clip,volume);
            }
            FlowRecord("ResultSfx key="+key+" clip="+id+" age="+RewardAge.ToString("F3",System.Globalization.CultureInfo.InvariantCulture));
        }
        void UpdateRewardPresentation() {
            if(!RaidRewardsVisible)return;
            if(rewardMusic!=null&&rewardMusic.isPlaying)rewardMusic.volume=.45f*Mathf.Clamp01(RewardAge/.8f);
            if(raidRewardsRevealAt<0)return;
            if(RewardAge>=1.2f)RewardSound("appear","bk_ui_card_appear",.38f);
            float countStart=raidRewardsRevealAt-raidRewardsAt-3;
            for(int step=0;step<3;step++)if(RewardAge>=countStart+step)RewardSound("count"+(3-step),"bk_ui_count",.65f,true);
            var entries=raidRewards.Entries.OrderBy(e=>e.Slot).ToArray();
            for(int i=0;i<entries.Length;i++) {
                float opened=Time.realtimeSinceStartup-raidRewardsRevealAt-i*1.8f;
                if(opened>=0)RewardSound("unlock"+entries[i].Slot,"bk_ui_card_unlock",.24f);
                if(opened>=.45f)RewardSound("open"+entries[i].Slot,"bk_ui_card_open",.48f);
            }
            for(int i=0;i<entries.Length;i++)if(Time.realtimeSinceStartup>=raidRewardsRevealAt+i*1.8f+1.1f&&rewardRevealed.Add(entries[i].Slot))
                FlowRecord("ResultCardRevealed slot="+entries[i].Slot+" age="+RewardAge.ToString("F3",System.Globalization.CultureInfo.InvariantCulture));
        }
        void RewardBurst(Vector2 center,float age,float strength=1) {
            if(age<0||age>1.2f)return;float p=age/1.2f,fade=1-p;
            TintTexture(glow,new Rect(center.x-170-p*80,center.y-95-p*45,340+p*160,190+p*90),new Color(1,.68f,.2f,fade*.45f*strength));
            for(int i=0;i<20;i++){float angle=i*2.39996f;var at=center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle)*.55f)*(22+p*(90+i%4*18));
                Fill(new Rect(at.x,at.y,2+i%3,2+i%2),new Color(1,.8f,.38f,fade*strength));}
        }

        Texture2D RewardTexture(string group, int index)
        {
            string key = group + "/" + index.ToString("000");
            Texture2D texture;
            if (!rewardTextures.TryGetValue(key, out texture))
            {
                texture = Resources.Load<Texture2D>("DawnRewards/" + key);
                rewardTextures[key] = texture;
            }
            return texture;
        }

        void RewardArt(string group, int index, Rect rect, float alpha = 1f)
        {
            var texture = RewardTexture(group, index);
            if (texture != null) TintTexture(texture, rect, new Color(1, 1, 1, alpha));
        }

        void RewardArtRegion(string group, int index, Rect rect, Rect pixels, float alpha = 1f)
        {
            var texture = RewardTexture(group, index);
            if (texture == null) return;
            GUI.color = new Color(1, 1, 1, alpha);
            GUI.DrawTextureWithTexCoords(rect, texture, new Rect(pixels.x / texture.width,
                1f - (pixels.y + pixels.height) / texture.height,
                pixels.width / texture.width, pixels.height / texture.height));
            GUI.color = Color.white;
        }

        void RewardSelfBorder(Rect card)
        {
            // The original highlight has an opaque black centre; its original
            // blend material was not recovered. Draw only its border strips
            // in IMGUI, so the contents and card back keep their original light.
            float opacity = .65f + Mathf.Sin(Time.realtimeSinceStartup * 2) * .15f;
            foreach (var pixels in new[] { new Rect(14, 14, 158, 9), new Rect(14, 107, 158, 9),
                new Rect(14, 23, 9, 84), new Rect(163, 23, 9, 84) })
                RewardArtRegion("bakal_reward", 4, new Rect(card.x + pixels.x * card.width / 186,
                    card.y + pixels.y * card.height / 130, pixels.width * card.width / 186,
                    pixels.height * card.height / 130), pixels, opacity);
        }

        // Call after GUI.matrix setup and before the normal flow/game UI.
        bool DrawRaidRewards()
        {
            if (!raidRewardsVisible || raidRewards == null) return false;
            var packet = raidRewards;
            float age = RewardAge;
            bool pending = packet.Status == "Pending";
            bool history = packet.Status == "History";
            int local = net != null ? net.LocalPlayerId : 0;
            if (history && flowMe != null)
            {
                // History may not have a combat slot in the current connection.
                var own = packet.Entries.FirstOrDefault(x => x.Name == flowCharacter);
                if (own != null) local = own.Slot;
            }
            Fill(new Rect(0, 0, 1280, 800), new Color(.045f, .022f, .025f, 1));
            RewardArt("bakal_reward", 12, new Rect(0, 0, 1280, 800));
            RewardArt("back_loop", (int)(age * 12) % 30, new Rect(0, 0, 1280, 800), .65f);
            RewardArt("bakal_reward", 17, new Rect(383, 120, 514, 485), .48f);
            Fill(new Rect(50, 78, 1180, 645), new Color(.025f, .018f, .018f, .72f));
            Fill(new Rect(90, 81, 1100, 1), new Color(.67f, .38f, .18f, .5f));
            Fill(new Rect(90, 720, 1100, 1), new Color(.67f, .38f, .18f, .5f));
            Label(new Rect(190, 91, 900, 28), "기계 혁명 : 바칼 레이드", 21, new Color(.84f, .7f, .48f), false, TextAnchor.MiddleCenter);
            // These static title elements have a 700px positioning canvas.
            // Fit the visible source region at its native aspect, not that canvas.
            RewardBurst(new Vector2(640,195),age,.7f);
            float titleAlpha=Mathf.Clamp01(age/.6f);
            RewardArtRegion("title", 2, new Rect(457, 125, 366, 366f * 11 / 227), new Rect(236, 76, 227, 11),titleAlpha);
            RewardArtRegion("bakal_reward", 8, new Rect(450, 152, 380, 380f * 44 / 238), new Rect(231, 0, 238, 44),titleAlpha);
            RewardArtRegion("title", 1, new Rect(452, 222, 376, 376f * 39 / 226), new Rect(236, 62, 226, 39),titleAlpha);
            float remaining=raidRewardsRevealAt-Time.realtimeSinceStartup;
            bool countdown=!history&&!pending&&remaining>0&&remaining<=3;
            if(countdown)DrawOriginalCountdown(Mathf.CeilToInt(remaining),Mathf.Repeat(3-remaining,1),new Vector2(640,286),106);
            else Label(new Rect(250, 275, 780, 40), history ? "최근 토벌 보상 기록" : pending ? "토벌 성공 · 보상을 기록하고 있습니다" : "공격대 보상",20,new Color(.95f,.85f,.65f),true,TextAnchor.MiddleCenter);

            var ordered=packet.Entries.OrderBy(e=>e.Slot).ToArray();
            if(age>=1.2f||history)for(int index=0;index<ordered.Length;index++) {
                var saved=GUI.matrix;float entrance=history?1:Mathf.SmoothStep(0,1,Mathf.Clamp01((age-1.2f)/1f));
                GUI.matrix=saved*Matrix4x4.Translate(new Vector3(0,(1-entrance)*38,0));
                DrawRaidRewardCard(ordered[index],index,ordered.Length,ordered[index].Slot==local,pending);
                GUI.matrix=saved;
            }

            Color status = pending ? new Color(.9f, .71f, .38f) : new Color(.6f, .85f, .65f);
            Label(new Rect(140, 592, 1000, 28), pending ? "보상 저장 중… 잠시만 기다려 주세요." : (packet.TotalTokens<0?"보상 지급 완료":"보상 지급 완료  ·  인장 보유량  " + packet.TotalTokens.ToString("N0")), 17, status, false, TextAnchor.MiddleCenter);
            Label(new Rect(140, 624, 1000, 24), pending ? "보상은 저장이 완료되면 자동으로 공개됩니다." : history ? "획득한 보상은 캐릭터에 저장됩니다." : Time.realtimeSinceStartup<RewardFinishedAt ? "공격대원의 보상을 순서대로 공개합니다" : "마을 복귀까지 "+Mathf.CeilToInt(Mathf.Max(0,15-(Time.realtimeSinceStartup-RewardFinishedAt)))+"초", 14, new Color(.66f, .62f, .59f), false, TextAnchor.MiddleCenter);
            if (rewardUiPreview)
                Label(new Rect(100, 22, 1080, 28), "UI 미리보기 · 가상 데이터 · 실제 보상 지급 없음", 16, Color.yellow, true, TextAnchor.MiddleCenter);

            bool enabled = GUI.enabled;
            GUI.enabled = enabled && !pending;
            if (GUI.Button(new Rect(505, 665, 270, 39), history || rewardUiPreview ? "확인" : "마을로 돌아가기", Style(17, new Color(1, .86f, .57f), true, TextAnchor.MiddleCenter)))
            {
                if (history || rewardUiPreview) HideRaidRewards();
                else if (net != null && net.IsDawn)
                {
                    net.Dawn.SendFlow(new C_RaidLeave { Encounter = packet.Encounter });
                    // Keep the result visible until the server confirms departure.
                }
            }
            Outline(new Rect(505, 665, 270, 39), new Color(.62f, .38f, .19f, pending ? .25f : .8f), 1);
            GUI.enabled = enabled;
            GUI.color = Color.white;
            return true;
        }

        void DrawRaidRewardCard(RaidReward entry, int index, int count, bool self, bool pending)
        {
            int slot=entry.Slot;float x = (1280-count*260)*.5f+index*260+5;
            Rect card = new Rect(x, 376, 250, 175);
            float opened = raidRewardsRevealAt < 0 ? -1 : Time.realtimeSinceStartup - raidRewardsRevealAt - index * 1.8f;
            bool shown = !pending && opened >= 1.1f;
            Color nameColor = self ? new Color(1, .81f, .44f) : new Color(.85f, .82f, .77f);
            string name = entry == null ? "참가자 없음" : entry.Name;
            var nameStyle = Style(15, nameColor, self, TextAnchor.MiddleCenter);
            string clipped = name;
            while (clipped.Length > 6 && nameStyle.CalcSize(new GUIContent(clipped)).x > 186)
                clipped = clipped.Substring(0, clipped.Length - 2) + "…";
            GUI.Label(new Rect(x + 45, 330, 194, 33), new GUIContent(clipped, name), nameStyle);
            if (self) Label(new Rect(x + 35, 359, 190, 18), "내 보상", 11, gold, false, TextAnchor.MiddleCenter);

            var player = sim == null ? null : sim.Players.FirstOrDefault(p => p.Id == slot);
            if (player != null && art.ContainsKey("player"))
            {
                Fill(new Rect(x + 8, 329, 34, 36), new Color(.15f, .1f, .085f));
                GUI.BeginGroup(new Rect(x + 8, 329, 34, 36));
                int frame = art["player"].clips.First(c => c.name == "idle").frames[0];
                DrawAvatar(player.Look, frame, new Vector2(17, 148), 1.3f, 1, Color.white, false, true);
                GUI.EndGroup();
            }

            if(opened>=0&&opened<1.1f){float pulse=Mathf.Sin(Mathf.Clamp01(opened/1.1f)*Mathf.PI);TintTexture(glow,new Rect(x-30,340,310,240),new Color(1,.66f,.18f,pulse*.65f));}
            RewardArt("bakal_reward", 0, card);
            if (!shown)
            {
                int frame = pending || opened < .45f ? 10 : Mathf.Clamp(14 + (int)((opened-.45f)/.65f*18), 14, 31);
                RewardArt(self ? "card_my" : "card_etc", frame, card);
            }
            else if (entry != null && entry.Eligible)
            {
                // Reuse the original dragon emblem and item-slot frame. The amount
                // is supplied exclusively by the server's persisted receipt.
                RewardArt("bakal_reward", 2, new Rect(x + 40, 407, 52, 52));
                RewardArt("card_my", 10, new Rect(x + 34, 407, 65, 47));
                Label(new Rect(x + 98, 408, 122, 25), "폭룡왕의 인장", 14, new Color(1, .8f, .47f), true, TextAnchor.MiddleLeft);
                Label(new Rect(x + 98, 439, 122, 34), "× " + entry.Amount.ToString("N0"), 25, Color.white, true, TextAnchor.MiddleLeft);
                if(entry.EquipmentTemplate>0){
                    var item=new ItemInfo{IconPath=entry.EquipmentIcon};var icon=OriginalItemIcon(item);
                    if(icon!=null)GUI.DrawTexture(new Rect(x+28,479,34,34),icon,ScaleMode.ScaleToFit);
                    Label(new Rect(x+68,479,157,22),ShortOriginalName(entry.EquipmentName,157,13),13,new Color(1,.75f,.35f),true);
                    Label(new Rect(x+68,502,157,18),"장비 획득 · 1개",11,new Color(.65f,.83f,.62f));
                }else Label(new Rect(x + 30, 484, 190, 24), "획득 완료", 13, new Color(.65f, .83f, .62f), false, TextAnchor.MiddleCenter);
            }
            else if (shown)
            {
                Label(new Rect(x + 25, 423, 200, 32), "보상 대상 아님", 16, new Color(.6f, .56f, .54f), false, TextAnchor.MiddleCenter);
                Label(new Rect(x + 20, 458, 210, 40), RewardExclusionText(entry == null ? "" : entry.Reason), 12, new Color(.6f, .56f, .54f), false, TextAnchor.MiddleCenter);
            }
            if(opened>=.45f&&opened<1.1f)DrawEffect("reward_newepicd01",opened-.45f,card.center,1.2f,1,Color.white,false,.65f);
            if(shown){
                RewardBurst(card.center,(opened-1.1f)*1.2f/.7f,.45f);
                DrawEffect("reward_newepicd02",opened-1.1f,card.center,.72f,1,Color.white,false,.28f);
            }
            if (self) RewardSelfBorder(card);
            if (pending) Label(new Rect(x + 20, 554, 210, 22), "기록 대기", 12, new Color(.65f, .58f, .5f), false, TextAnchor.MiddleCenter);
        }

        string RewardExclusionText(string reason)
        {
            if (reason == null) return "";
            string value = reason.ToLowerInvariant();
            if (value.Contains("expir")) return "재접속 기한 만료";
            if (value.Contains("leave") || value.Contains("left") || value.Contains("voluntary")) return "레이드에서 퇴장함";
            return "이번 토벌의 보상 조건 미충족";
        }

        // Optional visual fixture, isolated from transport and SQL. Parent calls
        // this from Awake after normal art/font setup, then returns when true.
        bool BeginRaidRewardsPreview()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--reward-ui-preview") < 0) return false;
            rewardUiPreview = true;
            PreloadRewardMusic();PreloadCountdownArt();
            sim = new RaidSimulation(4);
            string[] names = { "새벽의 버서커", "붉은달", "카잔의 후예", "폭룡왕 도전자" };
            var fixture = new S_RaidRewards { Encounter = 1, Status = "Saved", TotalTokens = 300 };
            for (int i = 0; i < 4; i++) fixture.Entries.Add(new RaidReward { Slot = i, PlayerDbId = i + 1, Name = names[i], Eligible = true, Amount = 100, Reason = "PreviewFixture",EquipmentTemplate=92001,EquipmentName="근원을 삼킨 도",EquipmentIcon="DawnEquipment/official-origin-katana" });
            ShowRaidRewards(fixture);
            StartCoroutine(CaptureRaidRewardsPreview());
            return true;
        }

        IEnumerator CaptureRaidRewardsPreview()
        {
            string output = Arg("--reward-preview-dir", Path.Combine(Application.persistentDataPath, "RewardUiPreview"));
            Directory.CreateDirectory(output);
            yield return new WaitForSecondsRealtime(.25f);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "reward-cards-closed.png"));
            yield return new WaitForSecondsRealtime(13.5f);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "reward-cards-open.png"));
            File.WriteAllText(Path.Combine(output, "FIXTURE.txt"), "Unity-rendered UI fixture only. No connection to game server or SQL; no real reward granted.");
            yield return new WaitForSecondsRealtime(1.5f);
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--reward-preview-exit") >= 0) Application.Quit();
        }
        IEnumerator CaptureSavedRaidReward(){
            while(raidRewardsVisible&&Time.realtimeSinceStartup<RewardFinishedAt+.4f)yield return null;
            yield return new WaitForEndOfFrame();
            if(!raidRewardsVisible || raidRewards.Status!="Saved")yield break;
            Directory.CreateDirectory(smokeDirectory);
            ScreenCapture.CaptureScreenshot(Path.Combine(smokeDirectory,"sql-saved-rewards.png"));
            File.WriteAllText(Path.Combine(smokeDirectory,"reward-receipt.json"),Google.Protobuf.JsonFormatter.Default.Format(raidRewards));
        }
    }
}
