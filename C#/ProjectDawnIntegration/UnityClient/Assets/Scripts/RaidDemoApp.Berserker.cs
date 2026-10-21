using System;
using System.Collections.Generic;
using UnityEngine;

namespace DnfRaidDemo
{
    // Berserker presentation: layered avatar, skill effects, DNF-style skill HUD, skill window and
    // inventory. All of it reads simulation snapshots only; combat stays in RaidSimulation.
    public sealed partial class RaidDemoApp
    {
        [Serializable] class BkLayer { public string resource; public int width, height, z; public int[] frames; }
        [Serializable] class BkOption { public string code; public BkLayer[] layers; }
        [Serializable] class BkPart { public string part, name; public BkOption[] options; }
        [Serializable] class BkWeapon { public string code, kind, name; public BkLayer[] layers; }
        [Serializable] class BkEffect { public string id, resource; public int width, height, frameCount; public float fps, canvasWidth, canvasHeight; public float[] anchor; public int[] frames; }
        [Serializable] class BkUi { public string name, resource; public int[] frames; }
        [Serializable] class BkClip { public string name; public int[] frames; public float[] durations; }
        [Serializable] class BkCut { public string id, resource; public float fps; public int[] sizes; }
        [Serializable] class BkFrenzy { public BkLayer back, front; public int[] poses, rearOnlyBodyFrames; }
        [Serializable] class BkBodyWeapon { public string effect; public int[] frames, frameOffsets; public float[] durations, offset; }
        [Serializable] class BkDoc { public string defaultLook; public float footX, footY; public BkPart[] parts; public BkWeapon[] weapons; public BkEffect[] effects; public BkUi[] ui; public BkClip[] skillClips; public BkCut[] cutscenes; public BkFrenzy frenzy; public BkBodyWeapon bloodSword; }

        // Atlas rows are flattened groups of 7: frame, atlasX, atlasY, w, h, canvasX, canvasY.
        sealed class Sheet
        {
            public Texture2D Texture; public int Width, Height; public int[] Rows;
            public readonly Dictionary<int, int> ByFrame = new Dictionary<int, int>();
            public int[] Order;
        }

        static readonly string[] PartOrder = { "weapon", "hair", "cap", "face", "neck", "coat", "pants", "belt", "shoes", "skin" };
        const string LookPref = "bk_look_v1", SlotPref = "bk_slots_v2";

        BkDoc bk;
        readonly Dictionary<string, Sheet> sheets = new Dictionary<string, Sheet>();
        readonly Dictionary<string, BkEffect> fx = new Dictionary<string, BkEffect>();
        readonly Dictionary<string, List<BkLayer>> lookLayers = new Dictionary<string, List<BkLayer>>();
        string localLook;
        readonly SkillId[] slots = new SkillId[14];
        bool skillWindow, inventoryWindow;
        string inventoryTab = "weapon";
        int selectedSkill = (int)SkillId.BloodSword, draggingSkill = -1;
        Vector2 inventoryScroll;
        readonly Color blood = new Color(.86f, .12f, .1f);

        void LoadBerserker()
        {
            TextAsset text = Resources.Load<TextAsset>("Art/berserker");
            bk = text != null ? JsonUtility.FromJson<BkDoc>(text.text) : new BkDoc();
            var extraText=Resources.Load<TextAsset>("AvatarImport/catalog");
            if(extraText!=null) {
                var extra=JsonUtility.FromJson<BkDoc>(extraText.text);
                var parts=new List<BkPart>(bk.parts??new BkPart[0]);
                foreach(var part in extra.parts??new BkPart[0]) {
                    var target=parts.Find(x=>x.part==part.part);
                    if(target==null)parts.Add(part);
                    else {var options=new List<BkOption>(target.options);foreach(var option in part.options)if(!options.Exists(x=>x.code==option.code))options.Add(option);target.options=options.ToArray();}
                }
                bk.parts=parts.ToArray();var weapons=new List<BkWeapon>(bk.weapons??new BkWeapon[0]);
                foreach(var weapon in extra.weapons??new BkWeapon[0])if(!weapons.Exists(x=>x.code==weapon.code))weapons.Add(weapon);
                bk.weapons=weapons.ToArray();
            }

            if (bk.effects != null) foreach (BkEffect effect in bk.effects) fx[effect.id] = effect;
            // Skill clips are stretched over each skill's action duration.
            Art player;
            if (bk.skillClips != null && art.TryGetValue("player", out player))
            {
                var clips = new List<Clip>(player.clips);
                foreach (BkClip clip in bk.skillClips)
                {
                    float duration = 1f;
                    foreach (SkillDef def in SkillBook.All) if (def.Clip == clip.name) duration = def.Duration;
                    clips.Add(new Clip { name = clip.name, frames = clip.frames, durations = clip.durations, fps = clip.frames.Length / Mathf.Max(.05f, duration) });
                }
                for (int i = 0; i < RaidFrenzy.ComboCount; i++)
                    clips.Add(new Clip { name = RaidFrenzy.Clip(i), frames = RaidFrenzy.Frames[i],
                        durations = RaidFrenzy.FrameTimes[i], fps = 20f });
                clips.Add(new Clip { name = RaidDashAttack.Clip, frames = RaidDashAttack.Frames,
                    durations = RaidDashAttack.FrameTimes, fps = 20f });
                player.clips = clips.ToArray();
            }
            localLook = PlayerPrefs.GetString(LookPref, bk.defaultLook ?? "");
            if (!LoadSlots()) DefaultSlots();
        }

        // ---------- look ----------
        static Dictionary<string, string> ParseLook(string look)
        {
            var map = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(look)) return map;
            foreach (string item in look.Split('|'))
            {
                int colon = item.IndexOf(':');
                if (colon > 0) map[item.Substring(0, colon)] = item.Substring(colon + 1);
            }
            return map;
        }

        static string JoinLook(Dictionary<string, string> map)
        {
            var parts = new List<string>();
            foreach (var pair in map) parts.Add(pair.Key + ":" + pair.Value);
            return string.Join("|", parts.ToArray());
        }

        List<BkLayer> LayersFor(string look)
        {
            if (string.IsNullOrEmpty(look)) look = bk.defaultLook ?? "";
            List<BkLayer> layers;
            if (lookLayers.TryGetValue(look, out layers)) return layers;
            layers = new List<BkLayer>();
            Dictionary<string, string> map = ParseLook(look);
            if (bk.parts != null)
                foreach (BkPart part in bk.parts)
                {
                    string code; if (!map.TryGetValue(part.part, out code)) continue;
                    foreach (BkOption option in part.options) if (option.code == code) layers.AddRange(option.layers);
                }
            string weapon;
            if (bk.weapons != null && map.TryGetValue("weapon", out weapon))
                foreach (BkWeapon item in bk.weapons) if (item.code == weapon) layers.AddRange(item.layers);
            layers.Sort((a, b) => a.z.CompareTo(b.z));
            if (lookLayers.Count > 64) lookLayers.Clear();
            lookLayers[look] = layers;
            return layers;
        }

        void Equip(string part, string code)
        {
            Dictionary<string, string> map = ParseLook(localLook);
            map[part] = code;
            localLook = JoinLook(map);
            if(flowMode)FlowSendAppearance();
            else {PlayerPrefs.SetString(LookPref, localLook); PlayerPrefs.Save();}
        }

        string Equipped(string part)
        {
            string code; return ParseLook(localLook).TryGetValue(part, out code) ? code : "";
        }

        // Keep each player's look current: local edits apply instantly, the partner's arrives by network.
        void SyncLooks()
        {
            if (sim == null || net == null) return;
            if (net.IsDawn) { if (!flowMode && sim.Players.Count > net.LocalPlayerId) sim.Players[net.LocalPlayerId].Look = localLook; return; }
            if (net.Role == NetworkRole.Client) { net.SetLocalLook(localLook); return; }
            sim.Looks[0] = localLook;
            if (sim.Players.Count > 0) sim.Players[0].Look = localLook;
            if (net.Role == NetworkRole.Host && sim.Players.Count > 1)
            {
                sim.Looks[1] = net.RemoteLook;
                sim.Players[1].Look = net.RemoteLook;
            }
        }

        // ---------- atlas drawing ----------
        Sheet GetSheet(string resource, int width, int height, int[] rows)
        {
            Sheet sheet;
            if (sheets.TryGetValue(resource, out sheet)) return sheet;
            sheet = new Sheet { Texture = LoadAtlas(resource), Width = width, Height = height, Rows = rows ?? new int[0] };
            var order = new List<int>();
            for (int i = 0; i + 6 < sheet.Rows.Length; i += 7) { sheet.ByFrame[sheet.Rows[i]] = i; order.Add(sheet.Rows[i]); }
            sheet.Order = order.ToArray();
            sheets[resource] = sheet;
            return sheet;
        }

        // Atlases are stored as PNG bytes and decoded when a look or effect is first drawn.
        static Texture2D LoadAtlas(string resource)
        {
            TextAsset data = Resources.Load<TextAsset>(resource);
            if (data == null) return null;
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            bool redBlood = UsesRedBloodPalette(resource);
            if (!texture.LoadImage(data.bytes, !redBlood)) return null;
            if (redBlood) ApplyRedBloodPalette(texture);
            Resources.UnloadAsset(data);
            return texture;
        }

        // Draws one atlas frame so that canvas point (originX, originY) lands on screen point "at".
        static void DrawRow(Sheet sheet, int row, Vector2 at, float originX, float originY, float scale, Color color)
        {
            if (sheet.Texture == null) return;
            int[] r = sheet.Rows;
            float w = r[row + 3], h = r[row + 4];
            var screen = new Rect(at.x + (r[row + 5] - originX) * scale, at.y + (r[row + 6] - originY) * scale, w * scale, h * scale);
            var uv = new Rect(r[row + 1] / (float)sheet.Width, 1f - (r[row + 2] + h) / sheet.Height, w / sheet.Width, h / sheet.Height);
            GUI.color = color;
            GUI.DrawTextureWithTexCoords(screen, sheet.Texture, uv);
            GUI.color = Color.white;
        }

        void DrawAvatar(string look, int frame, Vector2 foot, float scale, int facing, Color color, bool frenzy = false, bool hideWeapon = false)
        {
            Matrix4x4 saved = GUI.matrix;
            if (facing < 0) GUIUtility.ScaleAroundPivot(new Vector2(-1, 1), foot);
            bool rearSword = frenzy && bk.frenzy != null && bk.frenzy.rearOnlyBodyFrames != null
                && Array.IndexOf(bk.frenzy.rearOnlyBodyFrames, frame) >= 0;
            if (frenzy) DrawFrenzyLayer(frame, foot, scale, color, false);
            if (rearSword) DrawFrenzyLayer(frame, foot, scale, color, true);
            foreach (BkLayer layer in LayersFor(look))
            {
                if (hideWeapon && (layer.resource.StartsWith("Art/weapon/", StringComparison.Ordinal) || layer.resource.StartsWith("AvatarImport/weapon/", StringComparison.Ordinal))) continue;
                Sheet sheet = GetSheet(layer.resource, layer.width, layer.height, layer.frames);
                int row; if (sheet.ByFrame.TryGetValue(frame, out row)) DrawRow(sheet, row, foot, bk.footX, bk.footY, scale, color);
            }
            if (frenzy && !rearSword) DrawFrenzyLayer(frame, foot, scale, color, true);
            GUI.matrix = saved;
        }

        bool HasBloodSword(PlayerState p, float age)
        {
            return RaidMotion.BloodSwordWeapon(p, age) && bk.bloodSword != null
                && fx.ContainsKey(bk.bloodSword.effect);
        }

        // sword_normal.img uses a 281x142 local canvas, aligned with the 500x500 body canvas.
        // It replaces the equipped weapon during this action; it is not a free-running world FX.
        void DrawBloodSword(PlayerState p, Vector2 foot, Color tint, float age)
        {
            if (!HasBloodSword(p, age)) return;
            BkBodyWeapon weapon = bk.bloodSword;
            BkEffect effect = fx[weapon.effect];
            int frame = RaidMotion.Frame(weapon.frames, effect.fps, weapon.durations, age, true);
            Sheet sheet = GetSheet(effect.resource, effect.width, effect.height, effect.frames);
            int row; if (!sheet.ByFrame.TryGetValue(frame, out row)) return;
            Matrix4x4 saved = GUI.matrix;
            if (p.ActionFacing < 0) GUIUtility.ScaleAroundPivot(new Vector2(-1, 1), foot);
            float dx = weapon.offset[0], dy = weapon.offset[1];
            if (weapon.frameOffsets != null)
                for (int i = 0; i + 2 < weapon.frameOffsets.Length; i += 3)
                    if (weapon.frameOffsets[i] == frame) { dx += weapon.frameOffsets[i + 1]; dy += weapon.frameOffsets[i + 2]; break; }
            DrawRow(sheet, row, foot, bk.footX - dx, bk.footY - dy, 1.15f, tint);
            GUI.matrix = saved;
        }

        void DrawFrenzyLayer(int bodyFrame, Vector2 foot, float scale, Color color, bool front)
        {
            if (bk.frenzy == null || bk.frenzy.poses == null) return;
            BkLayer layer = front ? bk.frenzy.front : bk.frenzy.back;
            if (layer == null) return;
            // Poses: body frame, source sword frame, canvas offset x/y. Attack offsets remain zero.
            int[] poses = bk.frenzy.poses;
            for (int i = 0; i + 3 < poses.Length; i += 4)
                if (poses[i] == bodyFrame)
                {
                    Sheet sheet = GetSheet(layer.resource, layer.width, layer.height, layer.frames);
                    int row;
                    if (sheet.ByFrame.TryGetValue(poses[i + 1], out row))
                        DrawRow(sheet, row, foot, bk.footX - poses[i + 2], bk.footY - poses[i + 3], scale, color);
                    return;
                }
        }

        // Thumbnail of a single part option, centred in a box (inventory item icon).
        void DrawLayersInBox(BkLayer[] layers, Rect box, int frame)
        {
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            var found = new List<KeyValuePair<Sheet, int>>();
            var sorted = new List<BkLayer>(layers); sorted.Sort((a, b) => a.z.CompareTo(b.z));
            foreach (BkLayer layer in sorted)
            {
                Sheet sheet = GetSheet(layer.resource, layer.width, layer.height, layer.frames);
                int row; if (!sheet.ByFrame.TryGetValue(frame, out row)) continue;
                int[] r = sheet.Rows;
                minX = Mathf.Min(minX, r[row + 5]); minY = Mathf.Min(minY, r[row + 6]);
                maxX = Mathf.Max(maxX, r[row + 5] + r[row + 3]); maxY = Mathf.Max(maxY, r[row + 6] + r[row + 4]);
                found.Add(new KeyValuePair<Sheet, int>(sheet, row));
            }
            if (found.Count == 0) { Label(box, "없음", 12, Color.gray, false, TextAnchor.MiddleCenter); return; }
            float scale = Mathf.Min((box.width - 6) / Mathf.Max(1, maxX - minX), (box.height - 6) / Mathf.Max(1, maxY - minY), 1.6f);
            Vector2 at = new Vector2(box.center.x - (maxX - minX) * scale * .5f, box.center.y - (maxY - minY) * scale * .5f);
            foreach (var item in found) DrawRow(item.Key, item.Value, at, minX, minY, scale, Color.white);
        }

        // Plays an effect whose anchor sits on "point". Returns false once a one-shot effect has ended.
        bool DrawEffect(string id, float time, Vector2 point, float scale, int facing, Color color, bool loop, float duration = 0f)
        {
            BkEffect effect; if (time < 0 || !fx.TryGetValue(id, out effect)) return false;
            Sheet sheet = GetSheet(effect.resource, effect.width, effect.height, effect.frames);
            if (sheet.Order.Length == 0 || sheet.Texture == null) return false;
            // Old manifests have no count. Max ID + 1 preserves gaps even for that fallback.
            int count = effect.frameCount > 0 ? effect.frameCount : sheet.Order[sheet.Order.Length - 1] + 1;
            int frame = RaidMotion.AtlasFrame(count, effect.fps, time, loop, duration);
            if (frame < 0) return false;
            int row;
            if (!sheet.ByFrame.TryGetValue(frame, out row)) return true;
            Matrix4x4 saved = GUI.matrix;
            if (facing < 0) GUIUtility.ScaleAroundPivot(new Vector2(-1, 1), point);
            DrawRow(sheet, row, point, effect.anchor[0], effect.anchor[1], scale, color);
            GUI.matrix = saved;
            return true;
        }

        Texture2D Ui(string folder, int index)
        {
            string key = "Art/ui/" + folder + "/" + index.ToString("D4");
            Texture2D texture;
            if (!textures.TryGetValue(key, out texture)) { texture = Resources.Load<Texture2D>(key); textures[key] = texture; }
            return texture;
        }

        Texture2D SkillIcon(SkillDef def)
        {
            if (def == null || def.Icon == null) return null;
            string key = "Art/skillicon/" + def.Icon;
            Texture2D texture;
            if (!textures.TryGetValue(key, out texture)) { texture = Resources.Load<Texture2D>(key); textures[key] = texture; }
            return texture;
        }

        void UiDraw(string folder, int index, Rect rect, Color? color = null)
        {
            Texture2D texture = Ui(folder, index);
            if (texture != null) TintTexture(texture, rect, color ?? Color.white);
        }

        // In-game popup frame (9 pieces) over a dark fill.
        void Window(Rect r, string title)
        {
            Fill(new Rect(0, 0, 1280, 800), new Color(0, 0, 0, .35f));
            Fill(r, new Color(.03f, .03f, .045f, .93f));
            const float c = 6;
            UiDraw("window", 4, new Rect(r.x + c, r.y + c, r.width - 2 * c, r.height - 2 * c));
            UiDraw("window", 0, new Rect(r.x, r.y, c, c)); UiDraw("window", 1, new Rect(r.x + c, r.y, r.width - 2 * c, c)); UiDraw("window", 2, new Rect(r.xMax - c, r.y, c, c));
            UiDraw("window", 3, new Rect(r.x, r.y + c, c, r.height - 2 * c)); UiDraw("window", 5, new Rect(r.xMax - c, r.y + c, c, r.height - 2 * c));
            UiDraw("window", 6, new Rect(r.x, r.yMax - c, c, c)); UiDraw("window", 7, new Rect(r.x + c, r.yMax - c, r.width - 2 * c, c)); UiDraw("window", 8, new Rect(r.xMax - c, r.yMax - c, c, c));
            Fill(new Rect(r.x + 8, r.y + 8, r.width - 16, 30), new Color(.16f, .1f, .06f, .8f));
            Label(new Rect(r.x + 18, r.y + 10, r.width - 80, 26), title, 17, gold, true);
        }

        bool CloseButton(Rect window)
        {
            Rect close = new Rect(window.xMax - 36, window.y + 11, 24, 24);
            Texture2D x = Ui("common", close.Contains(Event.current.mousePosition) ? 7 : 6);
            if (x != null) GUI.DrawTexture(close, x, ScaleMode.ScaleToFit);
            return GUI.Button(close, GUIContent.none, GUIStyle.none);
        }

        // ---------- slots ----------
        bool LoadSlots()
        {
            string saved = PlayerPrefs.GetString(SlotPref, "");
            string[] items = saved.Split(',');
            if (items.Length != slots.Length) return false;
            for (int i = 0; i < slots.Length; i++)
            {
                int value; if (!int.TryParse(items[i], out value)) return false;
                slots[i] = value >= 0 && value < SkillBook.Count ? (SkillId)value : SkillId.Basic;
            }
            return true;
        }

        void DefaultSlots()
        {
            for (int i = 0; i < slots.Length; i++) slots[i] = SkillId.Basic;
            foreach (SkillDef def in SkillBook.All)
            {
                int index = Array.IndexOf(SkillBook.SlotKeys, def.Slot);
                if (index >= 0) slots[index] = def.Id;
            }
            SaveSlots();
        }

        void SaveSlots()
        {
            var values = new string[slots.Length];
            for (int i = 0; i < slots.Length; i++) values[i] = ((int)slots[i]).ToString();
            PlayerPrefs.SetString(SlotPref, string.Join(",", values)); PlayerPrefs.Save();
        }

        void Assign(int slot, SkillId skill)
        {
            for (int i = 0; i < slots.Length; i++) if (slots[i] == skill) slots[i] = slots[slot];
            slots[slot] = skill;
            SaveSlots();
        }

        // Latched slot presses since the last fixed step.
        int pressedSkills;
        void ReadSlotKeys()
        {
            for (int i = 0; i < slots.Length; i++)
                if (Input.GetKeyDown(SkillBook.SlotKeyCodes[i]) && !CommandKeyConsumed(SkillBook.SlotKeyCodes[i]) && slots[i] != SkillId.Basic) pressedSkills |= PlayerCommand.Bit(slots[i]);
        }

        // ---------- in-battle presentation ----------
        int PlayerFrame(RaidMotion.Pose pose)
        {
            Art entry; if (!art.TryGetValue("player", out entry)) return 39;
            Clip clip = entry.clips[0]; foreach (Clip candidate in entry.clips) if (candidate.name == pose.Clip) { clip = candidate; break; }
            return RaidMotion.Frame(clip.frames, clip.fps, clip.durations, pose.Time, pose.Once);
        }

        // ActionElapsed stops at the action's end, so effects keep their own clock (simulation time since the
        // cast). Each one-shot effect then plays to its last frame and disappears; loops stop at their Loop time.
        readonly Dictionary<int, KeyValuePair<int, float>> effectClock = new Dictionary<int, KeyValuePair<int, float>>();
        float EffectAge(PlayerState p)
        {
            KeyValuePair<int, float> clock;
            if (!effectClock.TryGetValue(p.Id, out clock) || clock.Key != p.ActionSequence || sim.Elapsed < clock.Value)
            {
                clock = new KeyValuePair<int, float>(p.ActionSequence, sim.Elapsed - p.ActionElapsed / p.ActionRate);
                effectClock[p.Id] = clock;
            }
            return (sim.Elapsed - clock.Value + PresentationExtrapolation())*p.ActionRate;
        }

        void DrawActionEffects(PlayerState p, Vector2 foot, bool behind)
        {
            DrawSkillTracks(p, foot, behind);
        }

        // 2·3차 각성: the transformation art replaces the avatar for its window of the action.
        bool DrawTransformation(PlayerState p, Vector2 foot, Color tint)
        {
            if (!SkillBook.Valid(p.ActiveSkill) || p.ActionDuration <= 0 || p.Hp <= 0) return false;
            SkillDef def = SkillBook.Get(p.ActiveSkill);
            float age = p.ActionElapsed + PresentationExtrapolation()*p.ActionRate;
            if (def.BodyFx == null || age < def.BodyFrom || age >= def.BodyUntil || age >= p.ActionDuration) return false;
            float duration = Mathf.Min(def.BodyUntil, p.ActionDuration) - def.BodyFrom;
            if (p.ActiveSkill == SkillId.Destruction) {
                BkEffect body;
                if (fx.TryGetValue(def.BodyFx, out body)) {
                    float frame = DestructionBodyFrame(age - def.BodyFrom);
                    return DrawEffect(def.BodyFx, frame / Mathf.Max(1, body.fps), foot, 1.15f, p.ActionFacing, tint, false);
                }
            }
            return DrawEffect(def.BodyFx, age - def.BodyFrom, foot, 1.15f, p.ActionFacing, tint, false, duration);
        }

        void DrawPlayerZoneEffect(AttackZone zone, Vector2 point)
        {
            if (zone.Skill < 0 || zone.Skill >= SkillBook.Count) return;
            SkillDef def = SkillBook.All[zone.Skill];
            float t = zone.Age - zone.Telegraph;
            if (def.ZoneFx != null) DrawEffect(def.ZoneFx, t, point, 1.45f, zone.Facing, new Color(1, 1, 1, .95f), true);
            else if (def.Id == SkillId.Basic) Sprite("skill_sidewind", "play", point, .9f, zone.Facing, t, WithAlpha(Color.white, .85f), true);
        }

        // Original awakening cut-ins: 1각 illustration slide, 2각 Blood Evil band, 3각 Neo full-screen scene.
        // Falls back to the nearest earlier frame that exists, so a missing file never blanks the scene.
        Texture2D CutFrame(BkCut cut, int index)
        {
            for (int i = index; i >= 0; i--)
            {
                string key = cut.resource + i.ToString("D4");
                Texture2D texture;
                if (!textures.TryGetValue(key, out texture)) { texture = LoadAtlas(key); textures[key] = texture; }
                if (texture != null) return texture;
            }
            return null;
        }

        // Decoding a 1067x600 frame on first display stalls that frame; decode every cut-in up front.
        bool cutscenesLoaded;
        void PreloadCutscenes()
        {
            if (cutscenesLoaded || bk == null || bk.cutscenes == null) return;
            cutscenesLoaded = true;
            foreach (BkCut cut in bk.cutscenes)
                for (int i = 0; i < (cut.sizes == null ? 0 : cut.sizes.Length / 2); i++) CutFrame(cut, i);
        }

        BkCut FindCut(string id)
        {
            if (bk.cutscenes != null) foreach (BkCut cut in bk.cutscenes) if (cut.id == id) return cut;
            return null;
        }

        void DrawAwakeningCut()
        {
            if (SuppressGalleryCutscenes) return;
            foreach (PlayerState p in sim.Players)
            {
                if (!SkillBook.Valid(p.ActiveSkill) || p.ActionDuration <= 0) continue;
                SkillDef def = SkillBook.Get(p.ActiveSkill);
                if (def.Kind != SkillKind.Awakening || p.ActiveSkill == SkillId.ExtremeOverkill && p.ActionOverkillFinish) continue;
                if(p.Id != (net?.LocalPlayerId??0)) {
                    if(flowMode && flowCutSeen.Add("remote/"+p.Id+"/"+p.ActionSequence))FlowRecord("CutsceneSuppressed actor="+p.Id+" local="+net.LocalPlayerId);
                    continue;
                }
                float age = EffectAge(p);
                // Gallery casting starts at 0.5 s: a 3 s action offset starts the movie
                // around video 3.5 s, during the held blood sword. Combat keeps advancing.
                if (def.Tier == 3) age -= 3f;
                if (age < 0) continue;
                BkCut cut = FindCut(def.Key);
                int count = cut != null && cut.sizes != null ? cut.sizes.Length / 2 : 0;
                float length = def.Tier == 1 ? .95f : count > 0 ? count / Mathf.Max(1f, cut.fps) : .9f;
                if (age > length) continue;
                RecordCutDraw(p.Id, (int)p.ActiveSkill, age);
                if(flowMode && flowCutSeen.Add("local/"+p.Id+"/"+p.ActionSequence))FlowRecord("CutsceneShown actor="+p.Id+" local="+net.LocalPlayerId);
                float a = def.Tier == 3 ? 1f : Mathf.Clamp01(Mathf.Min(age / .12f, (length - age) / .18f));
                if (def.Tier == 3 && count > 0)
                {
                    // Opaque mid-action overlay. Movie frame zero uses the offset clock;
                    // this never delays/restarts the skill, damage or server timeline.
                    Texture2D frame = CutFrame(cut, Mathf.Min(count - 1, Mathf.FloorToInt(age * cut.fps)));
                    if (frame != null) {
                        Fill(new Rect(0, 0, 1280, 720), Color.black);
                        TintTexture(frame, new Rect(0, 0, 1280, 720), Color.white);
                    }
                }
                else if (def.Tier == 2 && count > 0)
                {
                    int index = Mathf.Min(count - 1, Mathf.FloorToInt(age * cut.fps));
                    Texture2D frame = CutFrame(cut, index);
                    Fill(new Rect(0, 0, 1280, 720), new Color(.12f, 0, 0, .4f * a));
                    if (frame != null)
                    {
                        float h = 1280f * frame.height / frame.width;
                        TintTexture(frame, new Rect(0, 360 - h * .5f, 1280, h), WithAlpha(Color.white, a));
                    }
                }
                else
                {
                    Fill(new Rect(0, 0, 1280, 720), new Color(.12f, 0, 0, .45f * a));
                    Rect band = new Rect(0, 230, 1280, 170);
                    Fill(band, new Color(0, 0, 0, .7f * a));
                    Fill(new Rect(0, band.y, 1280, 2), WithAlpha(blood, a)); Fill(new Rect(0, band.yMax - 2, 1280, 2), WithAlpha(blood, a));
                    Texture2D art = count > 0 ? CutFrame(cut, 0) : null;
                    float slide = (1 - Mathf.Clamp01(age / .25f)) * -260;
                    if (art != null) TintTexture(art, new Rect(240 + slide, band.y - 70, 268, 239), WithAlpha(Color.white, a));
                }
                Label(new Rect(560, 610, 700, 30), def.Tier + "차 각성  ·  " + (p.Id + 1) + "P", 17, WithAlpha(gold, a), true, TextAnchor.UpperRight);
                Label(new Rect(360, 636, 900, 64), def.Name, 48, WithAlpha(Color.white, a), true, TextAnchor.UpperRight);
                return;
            }
        }

        // ---------- HUD ----------
        void DrawSkillHud(PlayerState local)
        {
            if(DrawOriginalSkillHud(local))return;
            Fill(new Rect(0, 708, 1280, 92), new Color(0, 0, 0, .55f));
            // Orbs: original HUD globes, filled from the bottom.
            DrawOrb(1, new Rect(330, 708, 88, 88), local.Hp / local.MaxHp, Mathf.CeilToInt(local.Hp / local.MaxHp * 100) + "%");
            DrawOrb(2, new Rect(862, 708, 88, 88), local.MaxMp > 0 ? local.Mp / local.MaxMp : 0, Mathf.FloorToInt(local.MaxMp > 0 ? local.Mp / local.MaxMp * 100 : 0) + "%");
            Fill(new Rect(424, 710, 432, 88), new Color(.04f, .04f, .06f, .9f));
            Outline(new Rect(424, 710, 432, 88), new Color(.35f, .27f, .17f), 1);
            for (int i = 0; i < slots.Length; i++)
            {
                Rect box = SlotRect(i);
                DrawSlot(box, slots[i], local, SkillBook.SlotKeys[i]);
            }
            // Active buff row above the slots.
            float bx = 428;
            if (local.FrenzyTime > 0) { DrawBuff(ref bx, SkillId.Frenzy, local.FrenzyTime); }
            if (local.BerserkTime > 0) { DrawBuff(ref bx, SkillId.Berserk, local.BerserkTime); }
            if (local.OverkillTime > 0) { DrawBuff(ref bx, SkillId.ExtremeOverkill, local.OverkillTime); }
        }

        void DrawBuff(ref float x, SkillId skill, float seconds)
        {
            Texture2D icon = SkillIcon(SkillBook.Get(skill));
            if (icon != null) GUI.DrawTexture(new Rect(x, 682, 24, 24), icon);
            Label(new Rect(x + 27, 685, 90, 20), SkillBook.Get(skill).Name + (skill==SkillId.Frenzy ? " ON" : seconds < 100 ? " " + Mathf.CeilToInt(seconds) + "s" : ""), 12, new Color(1, .5f, .45f), true);
            x += 110;
        }

        Rect SlotRect(int i) {return new Rect(459+(i%7)*52,703+(i/7)*36,50,34);}

        void DrawSlot(Rect box, SkillId skill, PlayerState local, string key)
        {
            Rect iconRect = new Rect(box.x + (box.width - box.height + 4) * .5f, box.y + 2, box.height - 4, box.height - 4);
            if(!OriginalHud("slotSmall",new Rect(iconRect.x-2,iconRect.y-2,iconRect.width+4,iconRect.height+4)))UiDraw("hud",33,box,Color.white);
            if (skill != SkillId.Basic)
            {
                SkillDef def = SkillBook.Get(skill);
                Texture2D icon = SkillIcon(def);
                bool armed = skill == SkillId.ExtremeOverkill && local != null && local.OverkillTime > 0;
                bool mpShort = !armed && local != null && local.Mp < def.Mp;
                if (icon != null) TintTexture(icon, iconRect, mpShort ? new Color(.45f, .55f, 1f) : Color.white);
                if (local != null)
                {
                    float cd = armed ? 0 : local.Cooldowns[(int)skill];
                    if (cd > 0)
                    {
                        float ratio = Mathf.Clamp01(cd / Mathf.Max(.01f, def.Cooldown));
                        Fill(new Rect(iconRect.x, iconRect.y, iconRect.width, iconRect.height * ratio), new Color(0, 0, 0, .68f));
                        Label(iconRect, cd < 10 ? cd.ToString("0.0") : Mathf.CeilToInt(cd).ToString(), 13, Color.white, true, TextAnchor.MiddleCenter);
                    }
                    else if (def.Kind == SkillKind.Awakening) Outline(iconRect, gold, armed ? 3 : 1);
                    if (armed) Label(iconRect, "재입력", 10, Color.white, true, TextAnchor.MiddleCenter);
                    if (skill==SkillId.Frenzy && cd<=0) Label(iconRect,local.FrenzyTime>0?"ON":"OFF",10,Color.white,true,TextAnchor.LowerCenter);
                }
            }
            Label(new Rect(box.xMax - 15, box.yMax - 15, 14, 14), key, 11, gold, true, TextAnchor.LowerRight);
        }

        void DrawOrb(int index, Rect rect, float fraction, string text)
        {
            Texture2D orb = Ui("orb", index);
            fraction = Mathf.Clamp01(fraction);
            TintTexture(Ui("hud", 4) ?? Texture2D.whiteTexture, rect, new Color(1, 1, 1, .9f));
            if (orb != null)
            {
                if(index==1&&sim!=null&&net!=null){float trail=Mathf.Clamp01(playerTrails[net.LocalPlayerId].Value);GUI.BeginGroup(new Rect(rect.x,rect.y+rect.height*(1-trail),rect.width,rect.height*trail));TintTexture(disc,new Rect(0,-rect.height*(1-trail),rect.width,rect.height),Color.white);GUI.EndGroup();}
                Rect part = new Rect(rect.x, rect.y + rect.height * (1 - fraction), rect.width, rect.height * fraction);
                GUI.DrawTextureWithTexCoords(part, orb, new Rect(0, 0, 1, fraction));
            }
            Label(rect, text, 15, Color.white, true, TextAnchor.MiddleCenter);
        }

        void DrawBossBar(Rect rect, float fraction)
        {
            if(originalHud.ContainsKey("bossRed")){
                OriginalHud("bossFrame",new Rect(rect.x-22,rect.y-18,rect.width+44,50));
                OriginalHud("bossBack",rect);OriginalFill("bossWhite",rect,bossTrail.Value,Color.white);OriginalFill("bossRed",rect,fraction,Color.white);return;
            }
            Texture2D frame = Ui("bosshp", 14), fill = Ui("bosshp", 7);
            Fill(rect, new Color(.06f, .04f, .04f, .9f));
            Fill(new Rect(rect.x,rect.y,rect.width*Mathf.Clamp01(bossTrail.Value),rect.height),Color.white);
            if (fill != null) TintTexture(fill, new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(fraction), rect.height), new Color(1f, .42f, .36f));
            else Fill(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(fraction), rect.height), new Color(.78f, .16f, .12f));
            if (frame != null) TintTexture(frame, new Rect(rect.x - 4, rect.y - 3, rect.width + 8, rect.height + 8), new Color(1, 1, 1, .85f));
        }

        // ---------- windows ----------
        bool AnyWindow { get { return skillWindow || inventoryWindow || commandWindow; } }

        void DrawSkillWindow()
        { DrawOriginalSkillWindow(); }

        void DrawInventoryWindow()
        { if (flowMode && !flowWardrobe) DrawOriginalEquipmentWindow(); else DrawOriginalAvatarWindow(); }

        static KeyValuePair<string, KeyValuePair<string, BkLayer[]>> Entry(string code, string name, BkLayer[] layers)
        { return new KeyValuePair<string, KeyValuePair<string, BkLayer[]>>(code, new KeyValuePair<string, BkLayer[]>(name, layers ?? new BkLayer[0])); }

        // --ui-review: captures menu, inventory, skill window and every skill in practice mode, then quits.
        bool uiReview;
        int reviewStep = -1;
        float reviewNext;
        void UpdateUiReview()
        {
            float age = Time.realtimeSinceStartup - startedAt;
            if (age < reviewNext) return;
            reviewStep++;
            switch (reviewStep)
            {
                case 0: reviewNext = 2f; return;
                case 1: Capture("ui-menu.png"); reviewNext = age + .3f; return;
                case 2: inventoryWindow = true; reviewNext = age + 1f; return;
                case 3: Capture("ui-inventory.png"); reviewNext = age + .3f; return;
                case 4: inventoryWindow = false; skillWindow = true; reviewNext = age + 1f; return;
                case 5: Capture("ui-skills.png"); reviewNext = age + .3f; return;
                case 6: skillWindow = false; Begin(NetworkRole.Offline); sim.PracticeInvulnerable = true; reviewNext = age + 1.2f; return;
            }
            int index = reviewStep - 7;
            var order = new List<SkillDef>();
            int only = Array.IndexOf(Environment.GetCommandLineArgs(), "--review-only");
            string onlyKey = only >= 0 && only + 1 < Environment.GetCommandLineArgs().Length ? Environment.GetCommandLineArgs()[only + 1] : null;
            foreach (SkillDef def in SkillBook.All)
                if (def.Kind != SkillKind.Basic && def.Kind != SkillKind.Dodge && (onlyKey == null || def.Key == onlyKey)) order.Add(def);
            if (index >= order.Count * 2) { Application.Quit(errors == 0 ? 0 : 1); return; }
            SkillDef skill = order[index / 2];
            PlayerState player = sim.Players[0];
            if (index % 2 == 0)
            {
                player.Position = sim.Boss.Position + new Vector2(-2.6f, 0f); player.Facing = 1;
                player.Mp = player.MaxMp;
                sim.Boss.Hp = sim.Boss.MaxHp * .9f;
                pressedSkills |= PlayerCommand.Bit(skill.Id);
                reviewNext = age + Mathf.Min(.9f, skill.Duration * .55f);
            }
            else { Capture("skill-" + skill.Key + ".png"); reviewNext = age + skill.Duration + .5f; }
        }

        string PartName(string part)
        {
            if (part == "weapon") return "무기";
            if (bk.parts != null) foreach (BkPart item in bk.parts) if (item.part == part) return item.name;
            return part;
        }
    }
}
