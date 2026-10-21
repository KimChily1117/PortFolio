using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Google.Protobuf.Protocol;
using UnityEngine;

namespace DnfRaidDemo
{
    public sealed partial class RaidDemoApp
    {
        readonly Dictionary<string, Texture2D> originalWindowTextures = new Dictionary<string, Texture2D>();
        Vector2 originalEquipmentScroll;
        int originalSelectedItem;
        bool originalWindowsPreview;
        int originalPreviewPage;
        readonly Color originalText = new Color(.82f, .8f, .74f);
        void ToggleOriginalInventory(bool avatar)
        {
            inventoryWindow = !(inventoryWindow && flowWardrobe == avatar);
            flowWardrobe = avatar; skillWindow = false;
        }

        Texture2D OriginalWindowTexture(string group, int frame)
        {
            string path = "DawnWindows/" + group + "/" + frame.ToString("000");
            Texture2D texture;
            if (!originalWindowTextures.TryGetValue(path, out texture))
                originalWindowTextures[path] = texture = Resources.Load<Texture2D>(path);
            return texture;
        }
        void OriginalPiece(string group, int frame, Rect rect)
        {
            var texture = OriginalWindowTexture(group, frame);
            if (texture != null) GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, true);
        }
        bool OriginalTab(Rect rect, string text, bool selected)
        {
            OriginalPiece("chrome", selected ? 13 : 15, rect);
            return GUI.Button(rect, text, Style(13, selected ? gold : originalText, selected, TextAnchor.MiddleCenter));
        }
        bool OriginalChrome(Rect r, string title, bool close = true)
        {
            Fill(r, new Color(.025f, .025f, .028f, .98f));
            const float c = 10;
            OriginalPiece("chrome", 4, new Rect(r.x + c, r.y + c, r.width - 2 * c, r.height - 2 * c));
            OriginalPiece("chrome", 0, new Rect(r.x, r.y, c, c)); OriginalPiece("chrome", 1, new Rect(r.x + c, r.y, r.width - 2 * c, c)); OriginalPiece("chrome", 2, new Rect(r.xMax - c, r.y, c, c));
            OriginalPiece("chrome", 3, new Rect(r.x, r.y + c, c, r.height - 2 * c)); OriginalPiece("chrome", 5, new Rect(r.xMax - c, r.y + c, c, r.height - 2 * c));
            OriginalPiece("chrome", 6, new Rect(r.x, r.yMax - c, c, c)); OriginalPiece("chrome", 7, new Rect(r.x + c, r.yMax - c, r.width - 2 * c, c)); OriginalPiece("chrome", 8, new Rect(r.xMax - c, r.yMax - c, c, c));
            OriginalPiece("chrome", 12, new Rect(r.x + 1, r.y + 1, r.width - 2, 24));
            Label(new Rect(r.x + 12, r.y + 2, r.width - 40, 22), title, 13, new Color(.95f, .84f, .54f), false, TextAnchor.MiddleCenter);
            if (!close) return false;
            Rect cross = new Rect(r.xMax - 20, r.y + 7, 12, 11);
            OriginalPiece("chrome", cross.Contains(Event.current.mousePosition) ? 43 : 42, cross);
            return GUI.Button(new Rect(r.xMax - 24, r.y + 1, 23, 24), GUIContent.none, GUIStyle.none);
        }
        bool OriginalActionButton(Rect r, string label)
        {
            OriginalPiece("chrome", !GUI.enabled ? 69 : r.Contains(Event.current.mousePosition) ? 67 : 66, r);
            return GUI.Button(r, label, Style(13, GUI.enabled ? Color.white : Color.gray, false, TextAnchor.MiddleCenter));
        }
        string ShortOriginalName(string text, float width, int size)
        {
            text = text ?? ""; var style = Style(size, Color.white);
            string result = text;
            while (result.Length > 4 && style.CalcSize(new GUIContent(result)).x > width)
                result = result.Substring(0, result.Length - 2) + "…";
            return result;
        }
        void OriginalItemSlot(Rect box, Texture2D icon, bool selected, bool equipped)
        {
            OriginalPiece("inventory", 49, box);
            if (icon != null) GUI.DrawTexture(new Rect(box.x + 3, box.y + 3, box.width - 6, box.height - 6), icon, ScaleMode.ScaleToFit);
            if (selected || box.Contains(Event.current.mousePosition)) Outline(box, selected ? gold : cyan, 1.5f);
            if (equipped) Label(new Rect(box.x + 2, box.y + 1, 15, 16), "E", 11, new Color(.25f, 1, .5f), true);
        }
        void OriginalCharacterPreview(Rect stage, bool avatar)
        {
            // Official avatar inventory shows the character on the left and a
            // compact equipment grid on the right. Clip oversized weapon art.
            GUI.BeginGroup(stage);
            float scale = stage.width / 248f;
            OriginalPiece("background", 1, new Rect(0, 0, stage.width, stage.height));
            var idle = art["player"].clips.FirstOrDefault(c => c.name == "idle");
            int frame = idle == null ? 39 : RaidMotion.Frame(idle.frames, idle.fps, idle.durations, Time.unscaledTime, false);
            OriginalPiece("inventory", 18, new Rect(3 * scale, 141 * scale, 100 * scale, 46 * scale));
            DrawAvatar(localLook, frame, new Vector2(58 * scale, 151 * scale), .95f * scale, 1, Color.white, false);
            Label(new Rect(106 * scale, 9 * scale, 137 * scale, 18 * scale), "버서커", 11, Color.white, true, TextAnchor.MiddleCenter);
            for (int i = 0; i < 10; i++)
            {
                Rect box = new Rect((110 + i % 4 * 32) * scale, (34 + i / 4 * 32) * scale, 29 * scale, 29 * scale);
                if (avatar)
                {
                    string part = PartOrder[i];
                    OriginalItemSlot(box, null, inventoryTab == part, false);
                    var layers = OriginalAvatarEntries(part).FirstOrDefault(e => e.Key == Equipped(part));
                    if (layers.Value.Value != null) DrawLayersInBox(layers.Value.Value, box, 39);
                    if (GUI.Button(box, new GUIContent("", PartName(part)), GUIStyle.none)) { inventoryTab = part; inventoryScroll = Vector2.zero; }
                }
                else
                {
                    var worn = flowItems.Where(v => v.Equipped).OrderBy(v => v.Slot).ToArray();
                    var item = i < worn.Length ? worn[i] : null;
                    OriginalItemSlot(box, item == null ? null : OriginalItemIcon(item), item != null && item.ItemDbId == originalSelectedItem, item != null);
                    if (item != null && GUI.Button(box, GUIContent.none, GUIStyle.none)) originalSelectedItem = item.ItemDbId;
                }
            }
            GUI.EndGroup();
        }
        Texture2D OriginalItemIcon(ItemInfo item)
        {
            string path = item.IconPath;
            if (string.IsNullOrEmpty(path) && flowCatalog != null)
                path = flowCatalog.weapons.Concat(flowCatalog.armors).FirstOrDefault(d => d.id == item.TemplateId)?.iconPath;
            if (string.IsNullOrEmpty(path)) return null;
            Texture2D texture;
            if (!originalWindowTextures.TryGetValue(path, out texture)) originalWindowTextures[path] = texture = Resources.Load<Texture2D>(path);
            return texture;
        }
        string OriginalItemName(ItemInfo item)
        {
            if (!string.IsNullOrEmpty(item.DisplayName)) return item.DisplayName;
            return flowCatalog?.weapons.Concat(flowCatalog.armors).FirstOrDefault(d => d.id == item.TemplateId)?.name ?? "장비 " + item.TemplateId;
        }
        void OriginalWindowHeading(Rect win, string title)
        {
            Window(win, title);
            if (CloseButton(win)) inventoryWindow = false;
            if (OriginalTab(new Rect(win.x + 18, win.y + 41, 114, 25), "장비", !flowWardrobe) && flowMode) flowWardrobe = false;
            if (OriginalTab(new Rect(win.x + 136, win.y + 41, 114, 25), "아바타", flowWardrobe || !flowMode)) flowWardrobe = true;
        }
        void OriginalEquipmentStats(Rect box)
        {
            OriginalChrome(box, "캐릭터 능력치", false);
            var stats = flowEquipmentStats;
            string[] labels = { "물리 공격력", "물리 방어력", "이동 속도", "공격 속도" };
            string[] values = { stats.PhysicalAttack.ToString("N0"), stats.PhysicalDefence.ToString("N0"), ((stats.MoveSpeedScale - 1) * 100).ToString("+0.#;-0.#;0") + "%", ((stats.AttackSpeedScale - 1) * 100).ToString("+0.#;-0.#;0") + "%" };
            for (int i = 0; i < labels.Length; i++)
            {
                float y = box.y + 38 + 27 * i;
                Label(new Rect(box.x + 12, y, 130, 23), labels[i], 13, originalText);
                Label(new Rect(box.x + 135, y, box.width - 147, 23), values[i], 13, Color.white, false, TextAnchor.UpperRight);
            }
        }

        void DrawOriginalEquipmentWindow()
        {
            Fill(new Rect(0, 0, 1280, 800), new Color(0, 0, 0, .22f));
            var items = flowItems.OrderBy(i => i.Slot).ThenBy(i => i.ItemDbId).ToArray();
            if (originalSelectedItem == 0 && items.Length > 0) originalSelectedItem = items[0].ItemDbId;
            Matrix4x4 saved = GUI.matrix;
            GUI.matrix = saved * Matrix4x4.TRS(new Vector3(686, 65, 0), Quaternion.identity, Vector3.one * 1.25f);
            if (OriginalChrome(new Rect(0, 0, 254, 526), "인벤토리 (I)")) inventoryWindow = false;
            OriginalTab(new Rect(3, 30, 74, 21), "장비", true);
            if (OriginalTab(new Rect(80, 30, 74, 21), "아바타", false)) flowWardrobe = true;
            OriginalCharacterPreview(new Rect(3, 55, 248, 179), false);
            OriginalPiece("chrome", 15, new Rect(3, 235, 248, 21));
            Label(new Rect(9, 235, 220, 21), "장비", 11, gold, true);
            int rows = Math.Max(8, (items.Length + 7) / 8);
            Rect grid = new Rect(3, 260, 248, 232);
            originalEquipmentScroll = GUI.BeginScrollView(grid, originalEquipmentScroll, new Rect(0, 0, 232, rows * 29));
            for (int i = 0; i < rows * 8; i++)
            {
                Rect box = new Rect(i % 8 * 29, i / 8 * 29, 28, 28);
                ItemInfo item = i < items.Length ? items[i] : null;
                OriginalItemSlot(box, item == null ? null : OriginalItemIcon(item), item != null && originalSelectedItem == item.ItemDbId, item != null && item.Equipped);
                if (item != null && GUI.Button(box, new GUIContent("", OriginalItemName(item)), GUIStyle.none)) originalSelectedItem = item.ItemDbId;
            }
            GUI.EndScrollView();
            Label(new Rect(9, 498, 233, 22), "보유 장비  " + items.Length, 11, originalText, false, TextAnchor.MiddleLeft);
            GUI.matrix = saved;
            Rect detail = new Rect(303, 65, 360, 437);
            OriginalChrome(detail, "장비 정보", false);
            var selected = items.FirstOrDefault(i => i.ItemDbId == originalSelectedItem);
            if (selected != null)
            {
                OriginalItemSlot(new Rect(detail.x + 18, detail.y + 44, 48, 48), OriginalItemIcon(selected), false, selected.Equipped);
                GUI.Label(new Rect(detail.x + 80, detail.y + 44, 260, 56), OriginalItemName(selected), new GUIStyle(Style(16, gold, true)) { wordWrap = true });
                Label(new Rect(detail.x + 18, detail.y + 117, 324, 23), "장비 정보", 14, Color.white, true);
                string[] lines = { "물리 공격력  +" + selected.PhysicalAttack, "물리 방어력  +" + selected.PhysicalDefence, "이동 속도  " + selected.MoveSpeedPercent.ToString("+0.#;-0.#;0") + "%", "공격 속도  " + selected.AttackSpeedPercent.ToString("+0.#;-0.#;0") + "%" };
                for (int i = 0; i < lines.Length; i++) Label(new Rect(detail.x + 18, detail.y + 155 + i * 30, 324, 23), lines[i], 14, originalText);
                Label(new Rect(detail.x + 18, detail.y + 295, 324, 24), selected.Equipped ? "현재 장착 중" : "보관 중", 13, selected.Equipped ? cyan : Color.gray);
                bool old = GUI.enabled; GUI.enabled = old && flowMode && flowStage == "Town" && !flowBusy && !originalWindowsPreview;
                if (OriginalActionButton(new Rect(detail.x + 18, detail.y + 342, 324, 31), selected.Equipped ? "장착 해제" : "장착")) net.Dawn.SendFlow(new C_EquipItem { ItemDbId = selected.ItemDbId, Equipped = !selected.Equipped });
                GUI.enabled = old;
                GUI.Label(new Rect(detail.x + 18, detail.y + 390, 324, 35), flowStage == "Raid" ? "던전: I → 아바타 탭 · U는 스킬 유지\n레이드 중 장비 변경 불가" : "I 장비 · U 아바타 · K 스킬\n장착 결과는 서버 저장 후 반영됩니다.", new GUIStyle(Style(12, originalText)) { wordWrap = true });
            }
            else Label(new Rect(detail.x + 20, detail.y + 55, 315, 60), "가방에서 장비를 선택하세요.", 14, originalText);
            OriginalEquipmentStats(new Rect(303, 522, 360, 200));
        }

        List<KeyValuePair<string, KeyValuePair<string, BkLayer[]>>> OriginalAvatarEntries(string partName)
        {
            var result = new List<KeyValuePair<string, KeyValuePair<string, BkLayer[]>>>();
            if (partName == "weapon")
            {
                foreach (var w in bk.weapons ?? new BkWeapon[0]) result.Add(Entry(w.code, w.name + " " + w.code.Substring(w.code.IndexOf(':') + 1), w.layers));
            }
            else foreach (var part in bk.parts ?? new BkPart[0]) if (part.part == partName)
                foreach (var option in part.options) result.Add(Entry(option.code, option.code == "" ? "해제" : part.name + " " + option.code, option.layers));
            return result;
        }
        void DrawOriginalAvatarWindow()
        {
            Fill(new Rect(0, 0, 1280, 800), new Color(0, 0, 0, .22f));
            var entries = OriginalAvatarEntries(inventoryTab);
            string current = Equipped(inventoryTab);
            Matrix4x4 saved = GUI.matrix;
            GUI.matrix = saved * Matrix4x4.TRS(new Vector3(686, 65, 0), Quaternion.identity, Vector3.one * 1.25f);
            if (OriginalChrome(new Rect(0, 0, 254, 526), flowStage == "Town" ? "아바타 인벤토리 (U)" : "아바타 인벤토리")) inventoryWindow = false;
            if (OriginalTab(new Rect(3, 30, 74, 21), "장비", false) && flowMode) flowWardrobe = false;
            OriginalTab(new Rect(80, 30, 74, 21), "아바타", true);
            OriginalCharacterPreview(new Rect(3, 55, 248, 179), true);
            OriginalPiece("chrome", 15, new Rect(3, 235, 248, 21));
            Label(new Rect(9, 235, 220, 21), PartName(inventoryTab) + " 아바타", 11, gold, true);
            int rows = Math.Max(8, (entries.Count + 7) / 8);
            inventoryScroll = GUI.BeginScrollView(new Rect(3, 260, 248, 232), inventoryScroll, new Rect(0, 0, 232, rows * 29));
            for (int i = 0; i < rows * 8; i++)
            {
                Rect box = new Rect(i % 8 * 29, i / 8 * 29, 28, 28);
                bool exists = i < entries.Count, worn = exists && entries[i].Key == current;
                OriginalItemSlot(box, null, worn, false);
                if (!exists) continue;
                DrawLayersInBox(entries[i].Value.Value, new Rect(box.x + 2, box.y + 2, 24, 24), 39);
                if (worn) Label(new Rect(box.x + 1, box.y, 12, 13), "E", 9, Color.green, true);
                bool can = !originalWindowsPreview && (!flowMode || flowStage == "Town" && !flowAppearancePending && !flowBusy);
                if (GUI.Button(box, new GUIContent("", entries[i].Value.Key), GUIStyle.none) && can) { Equip(inventoryTab, entries[i].Key); SyncLooks(); }
            }
            GUI.EndScrollView();
            Label(new Rect(9, 498, 233, 22), "보유 외형  " + entries.Count, 11, originalText);
            GUI.matrix = saved;
            Rect side = new Rect(303, 65, 360, 657);
            OriginalChrome(side, "아바타 정보", false);
            Label(new Rect(side.x + 18, side.y + 45, 324, 24), "아바타 부위", 16, gold, true);
            for (int i = 0; i < PartOrder.Length; i++)
                if (OriginalTab(new Rect(side.x + 18 + i % 2 * 164, side.y + 84 + i / 2 * 40, 156, 27), PartName(PartOrder[i]), inventoryTab == PartOrder[i])) { inventoryTab = PartOrder[i]; inventoryScroll = Vector2.zero; }
            Label(new Rect(side.x + 18, side.y + 304, 324, 25), "선택 부위  ·  " + PartName(inventoryTab), 14, Color.white, true);
            var chosen = entries.FirstOrDefault(e => e.Key == current);
            GUI.Label(new Rect(side.x + 18, side.y + 346, 324, 65), chosen.Value.Key ?? "기본 외형", new GUIStyle(Style(13, originalText)) { wordWrap = true });
            Label(new Rect(side.x + 18, side.y + 432, 324, 26), "보유 외형 " + entries.Count + "종", 14, originalText);
            GUI.Label(new Rect(side.x + 18, side.y + 479, 324, 53), "마을과 던전에서 같은 외형을 사용합니다.", new GUIStyle(Style(13, originalText)) { wordWrap = true });
            bool enabled = GUI.enabled; GUI.enabled = enabled && !originalWindowsPreview && (!flowMode || flowStage == "Town" && !flowAppearancePending && !flowBusy);
            if (OriginalActionButton(new Rect(side.x + 18, side.y + 550, 324, 31), "기본 외형으로")) { localLook = bk.defaultLook ?? ""; if (flowMode) FlowSendAppearance(); else { PlayerPrefs.SetString(LookPref, localLook); PlayerPrefs.Save(); } SyncLooks(); }
            GUI.enabled = enabled;
            GUI.Label(new Rect(side.x + 18, side.y + 607, 324, 35), flowAppearancePending ? "외형을 저장하고 있습니다…" : flowStage == "Raid" ? "아바타는 마을에서 변경할 수 있습니다." : "부위와 외형을 선택해 착용하세요.", new GUIStyle(Style(12, originalText)) { wordWrap = true });
        }

        void DrawOriginalSkillWindow()
        {
            Rect win = new Rect(150, 56, 980, 690);
            Fill(new Rect(0, 0, 1280, 800), new Color(0, 0, 0, .22f));
            if (OriginalChrome(win, "스킬 (K)  ·  버서커")) skillWindow = false;
            OriginalPiece("skill", 86, new Rect(163, 112, 954, 612));
            Label(new Rect(184, 116, 620, 28), "버서커  /  블러드 이블  /  진 버서커", 16, gold, true);
            if(OriginalActionButton(new Rect(860,116,230,26),"커맨드 설정 · F6")){OpenSkillCommands();return;}
            var skills = SkillBook.All.Where(s => s.Kind != SkillKind.Basic && s.Kind != SkillKind.Dodge).ToArray();
            PlayerState local = sim != null && net != null && sim.Players.Count > net.LocalPlayerId ? sim.Players[net.LocalPlayerId] : null;
            Event e = Event.current;
            var groups = new[] {
                skills.Where(d => d.Group == "전직 전" || d.Kind == SkillKind.Buff).ToArray(),
                skills.Where(d => d.Group == "전직 · 전투").ToArray(),
                skills.Where(d => (d.Group ?? "").Contains("1차")).ToArray(),
                skills.Where(d => (d.Group ?? "").Contains("2차")).ToArray(),
                skills.Where(d => (d.Group ?? "").Contains("3차")).ToArray()
            };
            string[] groupNames = { "공용\n보조", "버서커", "1차 각성", "2차 각성", "진 각성" };
            for (int row = 0; row < groups.Length; row++)
            {
                float y = 164 + row * 83;
                if (row > 0) Fill(new Rect(175, y - 5, 650, 1), new Color(.24f, .22f, .18f, .65f));
                Label(new Rect(178, y + 11, 72, 45), groupNames[row], 12, new Color(.62f, .55f, .38f), false, TextAnchor.MiddleCenter);
                for (int col = 0; col < groups[row].Length; col++)
                {
                    SkillDef def = groups[row][col];
                    Rect box = new Rect(265 + col * 80, y, 42, 56);
                    bool selected = selectedSkill == (int)def.Id;
                    OriginalPiece("skill", selected ? 80 : 18, box);
                    var icon = SkillIcon(def); if (icon != null) GUI.DrawTexture(new Rect(box.x + 3, box.y + 3, 36, 36), icon, ScaleMode.ScaleToFit);
                    Label(new Rect(box.x + 2, box.y + 40, 38, 15), def.Tier > 0 ? def.Tier + "차" : "사용", 10, def.Tier > 0 ? gold : new Color(.6f, .86f, .66f), false, TextAnchor.MiddleCenter);
                    GUI.Label(new Rect(box.x - 18, box.y + 58, 78, 18), new GUIContent(ShortOriginalName(def.Name, 78, 10), def.Name), Style(10, selected ? gold : originalText, selected, TextAnchor.MiddleCenter));
                    if (e.type == EventType.MouseDown && box.Contains(e.mousePosition)) { selectedSkill = (int)def.Id; draggingSkill = (int)def.Id; e.Use(); }
                }
            }
            SkillDef chosen = SkillBook.All[Mathf.Clamp(selectedSkill, 0, SkillBook.Count - 1)];
            Rect detail = new Rect(851, 144, 250, 449);
            var chosenIcon = SkillIcon(chosen); if (chosenIcon != null) GUI.DrawTexture(new Rect(detail.x + 12, detail.y + 12, 48, 48), chosenIcon);
            GUI.Label(new Rect(detail.x + 73, detail.y + 12, 165, 55), chosen.Name, new GUIStyle(Style(17, gold, true)) { wordWrap = true });
            Label(new Rect(detail.x + 12, detail.y + 78, 226, 24), chosen.Group ?? "액티브 스킬", 13, cyan);
            GUI.Label(new Rect(detail.x + 12, detail.y + 115, 226, 137), chosen.Description, new GUIStyle(Style(14, originalText)) { wordWrap = true });
            float damage = 0; foreach (var hit in chosen.Hits ?? new HitSpec[0]) damage += hit.Damage * Mathf.Max(1, hit.Repeat);
            string[] info = { "MP 소모  " + chosen.Mp, "재사용 대기 시간  " + chosen.Cooldown + "초", "물리 공격력  " + Mathf.RoundToInt(damage) + "%", chosen.Invulnerable > 0 ? "무적 시간  " + chosen.Invulnerable + "초" : "" };
            for (int i = 0; i < info.Length; i++) Label(new Rect(detail.x + 12, detail.y + 267 + i * 26, 226, 23), info[i], 13, originalText);
            if (local != null && local.Cooldowns[(int)chosen.Id] > 0) Label(new Rect(detail.x + 12, detail.y + 392, 226, 24), "남은 쿨타임  " + local.Cooldowns[(int)chosen.Id].ToString("0.0") + "초", 13, cyan);
            Label(new Rect(188, 597, 600, 24), "스킬 단축키  ·  아이콘을 끌어 놓거나 선택 후 슬롯 클릭", 13, originalText);
            for (int i = 0; i < slots.Length; i++)
            {
                Rect box = new Rect(188 + i * 45, 630, 40, 40);
                DrawSlot(box, slots[i], local, SkillBook.SlotKeys[i]);
                if (!box.Contains(e.mousePosition)) continue;
                Outline(box, gold, 1);
                if (e.type == EventType.MouseUp && draggingSkill >= 0) { Assign(i, (SkillId)draggingSkill); draggingSkill = -1; e.Use(); }
                else if (e.type == EventType.MouseDown && e.button == 1) { slots[i] = SkillId.Basic; SaveSlots(); e.Use(); }
                else if (e.type == EventType.MouseDown) { Assign(i, (SkillId)selectedSkill); e.Use(); }
            }
            if (OriginalActionButton(new Rect(865, 615, 220, 30), "기본 배치")) DefaultSlots();
            if (OriginalActionButton(new Rect(865, 660, 220, 30), "닫기")) skillWindow = false;
            Label(new Rect(188, 691, 625, 22), "슬롯 우클릭: 비우기  ·  X 기본 공격  ·  C 점프  ·  앞앞 달리기", 12, originalText);
            if (draggingSkill >= 0)
            {
                var icon = SkillIcon(SkillBook.All[draggingSkill]);
                if (icon != null) TintTexture(icon, new Rect(e.mousePosition.x - 20, e.mousePosition.y - 20, 40, 40), new Color(1, 1, 1, .8f));
                if (e.type == EventType.MouseUp) draggingSkill = -1;
            }
        }

        bool BeginOriginalWindowsPreview()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--original-windows-preview") < 0) return false;
            originalWindowsPreview = true; sim = new RaidSimulation(4); flowCharacter = "새벽의 버서커";
            flowCatalog = JsonUtility.FromJson<EquipmentCatalog>(Resources.Load<TextAsset>("DawnEquipment/catalog").text);
            int slot = 0;
            foreach (var def in flowCatalog.weapons.Concat(flowCatalog.armors).Take(16)) flowItems.Add(new ItemInfo { ItemDbId = ++slot, TemplateId = def.id, Slot = slot, Equipped = slot == 1, DisplayName = def.name, IconPath = def.iconPath, PhysicalAttack = def.damage, PhysicalDefence = def.defence, MoveSpeedPercent = def.moveSpeedPercent, AttackSpeedPercent = def.attackSpeedPercent });
            flowEquipmentStats = new S_EquipmentStats { PhysicalAttack = 100 + flowItems.Where(i => i.Equipped).Sum(i => i.PhysicalAttack), PhysicalDefence = flowItems.Where(i => i.Equipped).Sum(i => i.PhysicalDefence), MoveSpeedScale = 1 + flowItems.Where(i => i.Equipped).Sum(i => i.MoveSpeedPercent) / 100, AttackSpeedScale = 1 + flowItems.Where(i => i.Equipped).Sum(i => i.AttackSpeedPercent) / 100 };
            StartCoroutine(CaptureOriginalWindowsPreview()); return true;
        }
        bool DrawOriginalWindowsPreview()
        {
            if (!originalWindowsPreview) return false;
            Fill(new Rect(0, 0, 1280, 800), new Color(.05f, .045f, .05f));
            if (originalPreviewPage == 0) DrawOriginalEquipmentWindow();
            else if (originalPreviewPage == 1) DrawOriginalAvatarWindow(); else DrawOriginalSkillWindow();
            Label(new Rect(0, 18, 1280, 25), "UI 미리보기 · 가상 가방 데이터 · 실제 장착/저장 없음", 15, Color.yellow, true, TextAnchor.MiddleCenter);
            return true;
        }
        IEnumerator CaptureOriginalWindowsPreview()
        {
            string output = Arg("--window-preview-dir", Path.Combine(Application.persistentDataPath, "OriginalWindowsPreview")); Directory.CreateDirectory(output);
            string[] names = { "inventory", "avatar", "skills" };
            for (int i = 0; i < names.Length; i++)
            {
                originalPreviewPage = i; flowWardrobe = i == 1;
                yield return new WaitForSecondsRealtime(1.5f); yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(output, names[i] + ".png"));
            }
            File.WriteAllText(Path.Combine(output, "FIXTURE.txt"), "Unity UI preview using local catalog fixture. No server, SQL or real equipment mutation.");
            yield return new WaitForSecondsRealtime(1);
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--window-preview-exit") >= 0) Application.Quit();
        }
    }
}
