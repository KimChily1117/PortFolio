using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DnfRaidDemo
{
    // Presentation and input adapter. All combat decisions live in RaidSimulation.
    public sealed class RaidDemoApp : MonoBehaviour
    {
        [Serializable] class Clip { public string name; public float fps; public int[] frames; public float[] durations; }
        [Serializable] class Art { public string id, resourcePrefix; public int canvasWidth, canvasHeight; public float footX, footY; public Clip[] clips; }
        [Serializable] class Manifest { public Art[] actors, effects; }
        [Serializable] class SmokeResult
        {
            public string mode, role, networkStatus; public bool connected, phaseTwoExercised;
            public int ticks, players, errors, frames, guiRepaints, width, height; public float bossHp, elapsed, maximumJumpHeight;
            public Vector2 player0, player1;
            public float player0Hp, player1Hp;
            public bool practiceInvulnerable, showcase;
            public int showcasePatterns, player0Actions, player1Actions, groundJumpAvoids;
        }
        readonly Dictionary<string, Art> art = new Dictionary<string, Art>();
        readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        readonly Dictionary<string, GUIStyle> styles = new Dictionary<string, GUIStyle>();
        readonly Dictionary<int, Vector2> priorPositions = new Dictionary<int, Vector2>();
        readonly Dictionary<int, float> walkingUntil = new Dictionary<int, float>();
        readonly Dictionary<int, Vector2> displayPositions = new Dictionary<int, Vector2>();
        readonly Dictionary<int, float> displayHeights = new Dictionary<int, float>();
        readonly HashSet<string> recordedJumpAvoids = new HashSet<string>();
        readonly Dictionary<string, AudioClip> sounds = new Dictionary<string, AudioClip>();
        readonly HashSet<string> playedEvents = new HashSet<string>();
        sealed class ActorAnimation { public string Clip; public float Started, ActionTime; }
        readonly Dictionary<int, ActorAnimation> animations = new Dictionary<int, ActorAnimation>();
        RaidSimulation sim;
        RaidNetwork net;
        Texture2D arena, disc, ring, glow, vignette;
        Font font;
        AudioSource audioSource;
        bool inGame, showHelp, showBoxes, pauseMenu, practice;
        bool arc, wave, dash, awakening, jump;
        bool smoke, idleReview, showcase, smokePhase, smokeJump, capturedFirst, capturedSecond, capturedJump, smokeDone;
        int showcaseStage, lastPresentationTick = -1;
        float snapshotSeenAt;
        public RaidSimulation Simulation { get { return sim; } }
        public string CaptureRole { get { return net == null ? "none" : net.Role.ToString(); } }
        int errors, frameCount, guiRepaints;
        float accumulator, startedAt, smokeDuration = 16, finishAt;
        float maximumJumpHeight;
        string hostAddress = "127.0.0.1", portText = "27077", message = "";
        string smokeDirectory;
        Color gold = new Color(1f, .69f, .33f);
        Color cyan = new Color(.34f, .87f, 1f);
        Color ink = new Color(.035f, .045f, .065f, .94f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "--net-test-host") >= 0 || Array.IndexOf(args, "--net-test-client") >= 0) return;
            new GameObject("Raid demo").AddComponent<RaidDemoApp>();
        }

        void Awake()
        {
            Application.targetFrameRate = 60;
            Application.runInBackground = true;
            Application.logMessageReceived += OnLog;
            var cameraObject = new GameObject("Presentation camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            cameraObject.AddComponent<AudioListener>();
            audioSource = gameObject.AddComponent<AudioSource>(); audioSource.volume = .23f;
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 20);
            Manifest manifest = JsonUtility.FromJson<Manifest>(Resources.Load<TextAsset>("Art/manifest").text);
            foreach (Art entry in manifest.actors) art[entry.id] = entry;
            foreach (Art entry in manifest.effects) art[entry.id] = entry;
            arena = Resources.Load<Texture2D>("Art/arena/bakal_hall");
            disc = Radial(128, 0, false); ring = Radial(128, .88f, false); glow = Radial(128, 0, true);
            vignette = MakeVignette();
            sounds["Skill"] = Tone(160, .08f, .1f);
            sounds["Damage"] = Tone(90, .1f, .55f);
            sounds["Dodge"] = Tone(420, .10f, .02f);
            sounds["Jump"] = Tone(520, .12f, .05f);
            sounds["Phase"] = Tone(56, .8f, .2f);
            sounds["Stagger"] = Tone(330, .4f, .08f);
            string[] args = Environment.GetCommandLineArgs();
            idleReview = Array.IndexOf(args, "--idle-review") >= 0;
            showcase = Array.IndexOf(args, "--showcase") >= 0;
            smoke = Array.IndexOf(args, "--smoke") >= 0 || idleReview || showcase;
            smokeDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Logs"));
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--output") smokeDirectory = Path.GetFullPath(args[i + 1]);
                if (args[i] == "--port") portText = args[i + 1];
                if (args[i] == "--smoke-seconds") float.TryParse(args[i + 1], out smokeDuration);
            }
            startedAt = Time.realtimeSinceStartup;
            int joinIndex = Array.IndexOf(args, "--join");
            if (joinIndex >= 0 && joinIndex + 1 < args.Length) { hostAddress = args[joinIndex + 1]; Begin(NetworkRole.Client); }
            else if (Array.IndexOf(args, "--host") >= 0) Begin(NetworkRole.Host);
            else if (smoke && Array.IndexOf(args, "--menu-preview") < 0) Begin(NetworkRole.Offline);
            if (idleReview && sim != null)
            {
                sim.Mode = EncounterMode.Ready;
                sim.Boss.PatternName = "대기 모션 검토";
            }
        }

        void OnLog(string condition, string trace, LogType type)
        { if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) errors++; }

        void Begin(NetworkRole role)
        {
            try
            {
                int port;
                if (!int.TryParse(portText, out port) || port < 1024 || port > 65535) { message = "포트는 1024~65535 사이의 숫자로 입력하세요."; return; }
                if (net != null) net.Dispose();
                net = new RaidNetwork();
                sim = new RaidSimulation(role == NetworkRole.Offline ? 1 : 2);
                if (role == NetworkRole.Host) net.StartHost(port);
                else if (role == NetworkRole.Client) net.StartClient(hostAddress.Trim(), port);
                else sim.Start();
                if (role != NetworkRole.Offline && net.Role != role)
                { message = net.Status; net.Dispose(); net = null; inGame = false; return; }
                sim.PracticeInvulnerable = !showcase && ((practice && role == NetworkRole.Offline) || smoke);
                inGame = true; pauseMenu = false; showHelp = false; accumulator = 0;
                arc = wave = dash = awakening = jump = false;
                playedEvents.Clear(); priorPositions.Clear(); walkingUntil.Clear(); animations.Clear(); displayPositions.Clear(); displayHeights.Clear(); recordedJumpAvoids.Clear(); showcaseStage = 0; message = "";
            }
            catch (Exception ex) { message = "접속을 시작할 수 없습니다: " + ex.Message; if (net != null) net.Dispose(); net = null; inGame = false; }
        }

        void Update()
        {
            frameCount++;
            if (Input.GetKeyDown(KeyCode.F1)) showHelp = !showHelp;
            if (Input.GetKeyDown(KeyCode.F2)) showBoxes = !showBoxes;
            if (Input.GetKeyDown(KeyCode.Escape)) { if (inGame) pauseMenu = !pauseMenu; else showHelp = false; }
            if (Input.GetKeyDown(KeyCode.F12)) Capture("manual-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".png");
            if (inGame && sim != null)
            {
                net.Poll(sim);
                if (net.Role == NetworkRole.Host && net.Connected && sim.Mode == EncounterMode.Ready) sim.Start();
                if (!pauseMenu && !showHelp)
                {
                    arc |= Input.GetKeyDown(KeyCode.Q); wave |= Input.GetKeyDown(KeyCode.E);
                    dash |= Input.GetKeyDown(KeyCode.Space); awakening |= Input.GetKeyDown(KeyCode.R);
                    jump |= Input.GetKeyDown(KeyCode.K) || Input.GetKeyDown(KeyCode.C);
                }
                bool running = net.Role == NetworkRole.Offline ? !pauseMenu && !showHelp : net.Connected;
                if (running)
                {
                    accumulator += Mathf.Min(Time.unscaledDeltaTime, .12f);
                    while (accumulator >= RaidSimulation.FixedStep)
                    {
                        accumulator -= RaidSimulation.FixedStep;
                        PlayerCommand command = ReadCommand();
                        if (net.Role == NetworkRole.Client) net.SendInput(command);
                        else
                        {
                            var commands = new List<PlayerCommand> { command };
                            if (net.Role == NetworkRole.Host) commands.Add(net.ConsumeRemoteInput());
                            sim.Tick(RaidSimulation.FixedStep, commands);
                            if (net.Role == NetworkRole.Host) net.SendSnapshot(sim);
                        }
                    }
                }
                foreach (PlayerState player in sim.Players)
                {
                    maximumJumpHeight = Mathf.Max(maximumJumpHeight, player.JumpHeight);
                    Vector2 last;
                    if (priorPositions.TryGetValue(player.Id, out last) && (last - player.Position).sqrMagnitude > .00001f)
                        walkingUntil[player.Id] = WalkClock + .12f;
                    priorPositions[player.Id] = player.Position;
                    Vector2 previousDisplay;
                    if (net.Role == NetworkRole.Client && displayPositions.TryGetValue(player.Id, out previousDisplay) && (previousDisplay - player.Position).sqrMagnitude < 9)
                    {
                        float blend = 1 - Mathf.Exp(-Time.unscaledDeltaTime * 28);
                        displayPositions[player.Id] = Vector2.Lerp(previousDisplay, player.Position, blend);
                        displayHeights[player.Id] = Mathf.Lerp(displayHeights[player.Id], player.JumpHeight, blend);
                    }
                    else { displayPositions[player.Id] = player.Position; displayHeights[player.Id] = player.JumpHeight; }
                    foreach (AttackZone zone in sim.Zones)
                        if (!zone.FromPlayer && zone.GroundOnly && zone.Age >= zone.Telegraph && zone.Age < zone.Telegraph + zone.ActiveTime
                            && player.JumpHeight >= RaidSimulation.JumpDodgeHeight && player.Invulnerable <= 0
                            && RaidSimulation.Intersects(zone, player.Position, RaidSimulation.PlayerHurtboxHalfSize))
                            recordedJumpAvoids.Add(zone.Id + ":" + player.Id);
                }
                if (lastPresentationTick != sim.TickIndex) { lastPresentationTick = sim.TickIndex; snapshotSeenAt = Time.unscaledTime; }
                PlayEvents();
            }
            if (smoke) UpdateSmoke();
        }

        PlayerCommand ReadCommand()
        {
            var cmd = new PlayerCommand { PlayerId = net.Role == NetworkRole.Client ? 1 : 0 };
            if (!pauseMenu && !showHelp)
            {
                cmd.Move = new Vector2((Key(KeyCode.D, KeyCode.RightArrow) ? 1 : 0) - (Key(KeyCode.A, KeyCode.LeftArrow) ? 1 : 0),
                    (Key(KeyCode.W, KeyCode.UpArrow) ? 1 : 0) - (Key(KeyCode.S, KeyCode.DownArrow) ? 1 : 0));
                cmd.Basic = Input.GetKey(KeyCode.J); cmd.ArcSlash = arc; cmd.FlameWave = wave;
                cmd.Dash = dash; cmd.Awakening = awakening; cmd.Jump = jump;
            }
            if (showcase) cmd = RaidShowcase.Command(sim, cmd.PlayerId);
            else if (smoke && sim.Mode == EncounterMode.Fighting)
            {
                PlayerState p = sim.Players[Mathf.Min(cmd.PlayerId, sim.Players.Count - 1)];
                Vector2 goal = sim.Boss.Position + new Vector2(cmd.PlayerId == 0 ? -1.5f : -2.4f, cmd.PlayerId == 0 ? -.55f : .55f);
                cmd.Move = (goal - p.Position).magnitude > .15f ? (goal - p.Position).normalized : Vector2.zero;
                cmd.Basic = true; cmd.ArcSlash = p.Cooldowns[1] <= 0; cmd.FlameWave = p.Cooldowns[2] <= 0;
                cmd.Awakening = p.Cooldowns[4] <= 0;
                if (Time.realtimeSinceStartup - startedAt > 12.5f) cmd.Basic = cmd.ArcSlash = cmd.FlameWave = cmd.Awakening = false;
                foreach (AttackZone z in sim.Zones)
                    if (z.GroundOnly && z.Age > z.Telegraph - .32f && z.Age < z.Telegraph && p.JumpHeight <= .001f) cmd.Jump = true;
            }
            arc = wave = dash = awakening = jump = false;
            return cmd;
        }

        static bool Key(KeyCode a, KeyCode b) { return Input.GetKey(a) || Input.GetKey(b); }

        void PlayEvents()
        {
            foreach (CombatEvent ev in sim.Events)
            {
                // Event's originating tick stays stable in repeated snapshots.
                string id = ev.Kind + ":" + ev.TargetId + ":" + (sim.TickIndex - Mathf.RoundToInt(ev.Age * 60)) + ":" + ev.Position;
                if (playedEvents.Add(id) && ev.Age < .13f && sounds.ContainsKey(ev.Kind)) audioSource.PlayOneShot(sounds[ev.Kind]);
            }
            if (playedEvents.Count > 6000) playedEvents.Clear();
        }

        void UpdateSmoke()
        {
            float age = Time.realtimeSinceStartup - startedAt;
            if (showcase && sim != null && net.Role != NetworkRole.Client && sim.Mode == EncounterMode.Fighting)
            {
                if (!smokePhase && sim.Elapsed >= 23)
                { smokePhase = true; sim.Boss.Hp = Mathf.Min(sim.Boss.Hp, sim.Boss.MaxHp * .49f); }
                if (showcaseStage < RaidShowcase.PatternTimes.Length && sim.Elapsed >= RaidShowcase.PatternTimes[showcaseStage])
                { sim.ForcePattern(RaidShowcase.PatternIds[showcaseStage]); showcaseStage++; }
            }
            if (idleReview && sim != null)
            {
                sim.Boss.Phase = age >= 5 ? 2 : 1;
                if (age > 8 && !capturedSecond) { capturedSecond = true; Capture("idle-phase-2.png"); }
            }
            if (age > 4 && !capturedFirst) { capturedFirst = true; Capture("phase-1.png"); }
            if (!showcase && age > 8 && !smokePhase && sim != null && net.Role != NetworkRole.Client && sim.Mode == EncounterMode.Fighting)
            { smokePhase = true; sim.Boss.Hp = sim.Boss.MaxHp * .49f; }
            if (age > 12 && !capturedSecond) { capturedSecond = true; Capture("phase-2.png"); }
            if (!showcase && age > 12.6f && !smokeJump && sim != null && net.Role != NetworkRole.Client && sim.Mode == EncounterMode.Fighting)
            { smokeJump = true; sim.ForcePattern(5); }
            if (age > 13.56f && !capturedJump) { capturedJump = true; Capture("jump-pattern.png"); }
            if (age > smokeDuration && !smokeDone)
            {
                smokeDone = true; Directory.CreateDirectory(smokeDirectory);
                var result = new SmokeResult { mode = sim == null ? "menu" : sim.Mode.ToString(), role = net == null ? "none" : net.Role.ToString(),
                    networkStatus = net == null ? "" : net.Status, connected = net != null && net.Connected, ticks = sim == null ? 0 : sim.TickIndex,
                    players = sim == null ? 0 : sim.Players.Count, bossHp = sim == null ? 0 : sim.Boss.Hp, elapsed = sim == null ? 0 : sim.Elapsed,
                    errors = errors, frames = frameCount, guiRepaints = guiRepaints, width = Screen.width, height = Screen.height, phaseTwoExercised = sim != null && sim.Boss.Phase == 2,
                    maximumJumpHeight = maximumJumpHeight,
                    player0 = sim == null ? Vector2.zero : sim.Players[0].Position,
                    player1 = sim != null && sim.Players.Count > 1 ? sim.Players[1].Position : Vector2.zero,
                    player0Hp = sim == null ? 0 : sim.Players[0].Hp, player1Hp = sim != null && sim.Players.Count > 1 ? sim.Players[1].Hp : 0,
                    practiceInvulnerable = sim != null && sim.PracticeInvulnerable, showcase = showcase,
                    showcasePatterns = showcaseStage, groundJumpAvoids = recordedJumpAvoids.Count,
                    player0Actions = sim == null ? 0 : sim.Players[0].ActionSequence,
                    player1Actions = sim != null && sim.Players.Count > 1 ? sim.Players[1].ActionSequence : 0 };
                File.WriteAllText(Path.Combine(smokeDirectory, "runtime-smoke.json"), JsonUtility.ToJson(result, true));
                Debug.Log("[RAID_RUNTIME_SMOKE] " + JsonUtility.ToJson(result)); finishAt = age + 1;
            }
            if (smokeDone && age > finishAt) Application.Quit(errors == 0 ? 0 : 1);
        }

        void Capture(string name)
        {
            Directory.CreateDirectory(smokeDirectory);
            ScreenCapture.CaptureScreenshot(Path.Combine(smokeDirectory, name));
        }

        void OnGUI()
        {
            if (Event.current.type == EventType.Repaint) guiRepaints++;
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 800f);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1280 * scale) * .5f, (Screen.height - 800 * scale) * .5f, 0), Quaternion.identity, new Vector3(scale, scale, 1));
            GUI.color = Color.white;
            if (arena != null) GUI.DrawTexture(new Rect(0, 0, 1280, 720), arena);
            GUI.DrawTexture(new Rect(0, 0, 1280, 800), vignette);
            if (inGame && sim != null) DrawGame(); else DrawMenu();
            if (showHelp) DrawHelp();
        }

        void DrawMenu()
        {
            Fill(new Rect(0, 0, 1280, 800), new Color(0, .015f, .035f, .22f));
            Sprite("boss1", "idle", new Vector2(945, 620), 1.38f, -1, Time.unscaledTime, Color.white);
            Label(new Rect(76, 95, 700, 30), "DNF  /  RAID RECONSTRUCTION", 17, gold);
            Label(new Rect(70, 139, 740, 90), "폭룡왕 바칼", 64, Color.white, true);
            Label(new Rect(77, 236, 700, 42), "성전에서 마주하는 두 명의 도전자", 24, new Color(.8f, .83f, .86f));
            Fill(new Rect(78, 307, 55, 3), gold);
            Label(new Rect(77, 332, 660, 68), "예고를 읽고, 화염을 피하고, 빈틈을 베어내세요.\n6가지 패턴 · 2단계 전투 · 스킬과 점프 회피", 18, new Color(.75f, .79f, .84f));
            if (Button(new Rect(78, 438, 246, 56), "혼자 도전", true)) Begin(NetworkRole.Offline);
            if (Button(new Rect(336, 438, 246, 56), "2인 협동 방 만들기")) Begin(NetworkRole.Host);
            Label(new Rect(80, 514, 60, 32), "주소", 16, Color.white);
            hostAddress = GUI.TextField(new Rect(136, 510, 195, 38), hostAddress, 80, Style(17, Color.white, false, TextAnchor.MiddleLeft, true));
            Label(new Rect(348, 514, 55, 32), "포트", 16, Color.white);
            portText = GUI.TextField(new Rect(400, 510, 92, 38), portText, 5, Style(17, Color.white, false, TextAnchor.MiddleLeft, true));
            if (Button(new Rect(78, 565, 504, 49), "주소로 협동 참가")) Begin(NetworkRole.Client);
            if (Button(new Rect(78, 630, 350, 36), (practice ? "[ON]  " : "[OFF]  ") + "혼자 연습 무적", practice)) practice = !practice;
            if (Button(new Rect(78, 698, 180, 38), "조작 안내  F1")) showHelp = true;
            if (Button(new Rect(270, 698, 180, 38), "종료")) Application.Quit();
            Label(new Rect(80, 753, 1070, 28), string.IsNullOrEmpty(message) ? "LOCAL DEMO  v0.1.2     |     직접 IP 연결 · 호스트 판정 · Windows" : message, 13, string.IsNullOrEmpty(message) ? Color.gray : gold);
        }

        Vector2 ScreenPoint(Vector2 position) { return new Vector2(640 + position.x * 56, 565 - position.y * 32); }

        void DrawGame()
        {
            // Ground telegraphs are drawn before actors; depth order follows arena Y.
            foreach (AttackZone zone in sim.Zones) if (zone.Age >= 0) DrawZone(zone);
            var order = new List<PlayerState>(sim.Players);
            order.Sort((a, b) => b.Position.y.CompareTo(a.Position.y));
            bool bossDrawn = false;
            foreach (PlayerState p in order)
            {
                if (!bossDrawn && sim.Boss.Position.y >= p.Position.y) { DrawBoss(); bossDrawn = true; }
                DrawPlayer(p);
            }
            if (!bossDrawn) DrawBoss();
            foreach (AttackZone zone in sim.Zones)
            {
                if (zone.FromPlayer || zone.GroundOnly || zone.Age < zone.Telegraph || zone.Age > zone.Telegraph + zone.ActiveTime + .25f) continue;
                Vector2 point = ScreenPoint(zone.Center);
                if (zone.Shape == ZoneShape.Circle)
                    Sprite("boss_firepole", "play", point, Mathf.Min(.8f, zone.Radius * .32f), 1, zone.Age - zone.Telegraph, new Color(1, 1, 1, .85f));
                else if (zone.Shape == ZoneShape.Box)
                    for (int i = -2; i <= 2; i++)
                        Sprite("boss_breath", "play", point + new Vector2(i * zone.HalfSize.x * 24, -30), .8f, 1, zone.Age - zone.Telegraph + i * .04f, new Color(1, 1, 1, .8f));
            }
            foreach (CombatEvent ev in sim.Events) DrawEvent(ev);
            DrawHud();
            if (showcase) Label(new Rect(24, 135, 1100, 25), "자동 회피 시연 · 연습 무적 OFF · 패턴/2페이즈 시연 호출", 13, Color.white);
            if (net.Role != NetworkRole.Offline && !net.Connected)
            {
                Panel(new Rect(365, 250, 550, 190));
                Label(new Rect(380, 273, 520, 36), net.Role == NetworkRole.Host ? "함께할 도전자를 기다립니다" : "호스트에 연결 중", 25, Color.white, true, TextAnchor.MiddleCenter);
                Label(new Rect(390, 320, 500, 54), net.Status + "\nUDP " + portText + "  ·  같은 네트워크에서 호스트 IP로 참가", 15, cyan, false, TextAnchor.MiddleCenter);
                if (Button(new Rect(520, 385, 240, 38), "로비로 돌아가기")) ReturnMenu();
            }
            else if (sim.Mode == EncounterMode.Victory || sim.Mode == EncounterMode.Defeat) DrawEnd();
            if (pauseMenu) DrawPause();
        }

        void DrawBoss()
        {
            BossState b = sim.Boss;
            Vector2 p = ScreenPoint(b.Position);
            TintTexture(glow, new Rect(p.x - 100, p.y - 25, 200, 50), new Color(.8f, .1f, 0, .4f));
            bool attacking = b.ActionDuration > 0 && b.ActionElapsed < b.ActionDuration;
            string clip = b.Hp <= 0 ? "death" : b.StaggerTime > 0 ? "hurt" : b.TransitionTime > 0 ? "cast" : attacking ? "attack" : "idle";
            PlayerState target = null;
            foreach (PlayerState candidate in sim.Players)
                if (candidate.Hp > 0 && (target == null || (candidate.Position - b.Position).sqrMagnitude < (target.Position - b.Position).sqrMagnitude)) target = candidate;
            int facing = target != null && target.Position.x > b.Position.x ? 1 : -1;
            string actor = b.Phase == 2 ? "boss2" : "boss1";
            float time = clip == "attack" ? Mathf.Min(1, (b.ActionElapsed + PresentationExtrapolation()) / b.ActionDuration) * ClipDuration(actor, clip)
                : clip == "cast" ? (1.2f - b.TransitionTime) / 1.2f * ClipDuration(actor, clip) : AnimationTime(-1, clip + b.Phase, 0);
            Sprite(actor, clip, p, b.Phase == 2 ? .66f : 1.04f, facing, time, Color.white, clip == "death" || clip == "attack" || clip == "cast" || clip == "hurt");
            if (showBoxes) DrawHurtbox(p, RaidSimulation.BossHurtboxHalfSize, gold);
        }

        void DrawPlayer(PlayerState p)
        {
            Vector2 displayed; if (!displayPositions.TryGetValue(p.Id, out displayed)) displayed = p.Position;
            Vector2 pos = ScreenPoint(displayed);
            Color color = p.Id == 0 ? cyan : new Color(.72f, .59f, 1);
            TintTexture(disc, new Rect(pos.x - 22, pos.y - 6, 44, 13), new Color(0, 0, 0, .6f));
            TintTexture(ring, new Rect(pos.x - 25, pos.y - 8, 50, 16), color);
            float until; bool walking = walkingUntil.TryGetValue(p.Id, out until) && until > WalkClock;
            RaidMotion.Pose pose = RaidMotion.Player(p, walking, 0, PresentationExtrapolation());
            if (pose.Clip == "idle" || pose.Clip == "walk" || pose.Clip == "hurt" || pose.Clip == "death")
                pose.Time = AnimationTime(p.Id, pose.Clip, 0);
            Color tint = p.Invulnerable > 0 && Mathf.Sin(Time.unscaledTime * 45) > 0 ? new Color(.5f, .9f, 1, .6f) : Color.white;
            if (p.Hp <= 0) tint = new Color(.5f, .5f, .55f, .65f);
            float height; if (!displayHeights.TryGetValue(p.Id, out height)) height = p.JumpHeight;
            Sprite("player", pose.Clip, pos - new Vector2(0, height * 65), 1.15f, pose.Facing, pose.Time, tint, pose.Once);
            Label(new Rect(pos.x - 42, pos.y + 10, 84, 23), (p.Id + 1) + "P" + (p.Id == net.LocalPlayerId ? "  YOU" : ""), 12, color, true, TextAnchor.MiddleCenter);
            if (showBoxes) DrawHurtbox(pos, RaidSimulation.PlayerHurtboxHalfSize, color);
        }

        void DrawHurtbox(Vector2 foot, Vector2 half, Color color)
        { Outline(new Rect(foot.x - half.x * 56, foot.y - half.y * 32, half.x * 112, half.y * 64), color, 2); }

        void DrawZone(AttackZone zone)
        {
            Vector2 p = ScreenPoint(zone.Center);
            bool active = zone.Age >= zone.Telegraph && zone.Age <= zone.Telegraph + zone.ActiveTime;
            bool recovery = zone.Age > zone.Telegraph + zone.ActiveTime;
            Color color = zone.FromPlayer ? cyan : active ? new Color(1, .23f, .08f) : gold;
            if (zone.GroundOnly) color = active ? new Color(.9f, .55f, 1) : new Color(.65f, .5f, 1);
            float alpha = recovery ? .09f : active ? .5f : .12f + .07f * Mathf.Sin(Time.unscaledTime * 7);
            if (zone.Shape == ZoneShape.Box)
            {
                Rect r = new Rect(p.x - zone.HalfSize.x * 56, p.y - zone.HalfSize.y * 32, zone.HalfSize.x * 112, zone.HalfSize.y * 64);
                Fill(r, WithAlpha(color, alpha)); Outline(r, WithAlpha(color, recovery ? .2f : .9f), active ? 3 : 2);
                if (!active && !recovery && zone.Telegraph > 0) Fill(new Rect(r.x, r.yMax - 3, r.width * Mathf.Clamp01(zone.Age / zone.Telegraph), 3), color);
            }
            else
            {
                Rect r = new Rect(p.x - zone.Radius * 56, p.y - zone.Radius * 32, zone.Radius * 112, zone.Radius * 64);
                if (zone.Shape == ZoneShape.Donut)
                {
                    string key = "donut-" + zone.InnerRadius + "-" + zone.Radius;
                    Texture2D tex;
                    if (!textures.TryGetValue(key, out tex)) { tex = Radial(256, zone.InnerRadius / zone.Radius, false); textures[key] = tex; }
                    TintTexture(tex, r, WithAlpha(color, alpha));
                    Rect safe = new Rect(p.x - zone.InnerRadius * 56, p.y - zone.InnerRadius * 32, zone.InnerRadius * 112, zone.InnerRadius * 64);
                    TintTexture(ring, safe, cyan);
                    Label(new Rect(p.x - 50, p.y - 14, 100, 28), "안전 지대", 14, cyan, true, TextAnchor.MiddleCenter);
                }
                else TintTexture(disc, r, WithAlpha(color, alpha));
                TintTexture(ring, r, WithAlpha(color, recovery ? .2f : .95f));
                if (!active && !recovery && zone.Telegraph > 0)
                {
                    float ratio = Mathf.Clamp01(zone.Age / zone.Telegraph);
                    TintTexture(ring, new Rect(p.x - r.width * ratio * .5f, p.y - r.height * ratio * .5f, r.width * ratio, r.height * ratio), WithAlpha(color, .65f));
                }
            }
            if (zone.FromPlayer && active)
            {
                int facing = zone.Velocity.x < 0 ? -1 : 1;
                if (zone.Velocity.x == 0) foreach (PlayerState player in sim.Players) if (player.Id == zone.OwnerId) facing = player.Facing;
                Sprite(zone.Shape == ZoneShape.Box ? "skill_sidewind" : "skill_ghostslash", "play", p, .9f, facing, zone.Age - zone.Telegraph, WithAlpha(Color.white, .85f), true);
            }
        }

        void DrawEvent(CombatEvent ev)
        {
            if (ev.Kind != "Damage" || ev.Age > 1f) return;
            Vector2 p = ScreenPoint(ev.Position);
            Color color = ev.TargetId == -1 ? new Color(1, .9f, .57f) : new Color(1, .36f, .33f);
            color.a = 1 - ev.Age;
            Label(new Rect(p.x - 65 + Mathf.Sin(ev.Value) * 22, p.y - 145 - ev.Age * 52, 130, 42), Mathf.RoundToInt(ev.Value).ToString(), ev.Value >= 500 ? 30 : 23, color, true, TextAnchor.MiddleCenter);
        }

        void DrawHud()
        {
            Fill(new Rect(0, 0, 1280, 130), new Color(.02f, .025f, .04f, .85f));
            Label(new Rect(45, 20, 170, 24), "BAKAL RAID", 14, gold, true);
            Label(new Rect(45, 48, 160, 37), "폭룡왕 바칼", 25, Color.white, true);
            Label(new Rect(225, 20, 620, 25), "PHASE " + sim.Boss.Phase + "   /   " + sim.Boss.PatternName, 17, Color.white, true);
            Bar(new Rect(225, 53, 830, 20), sim.Boss.Hp / sim.Boss.MaxHp, new Color(.78f, .16f, .12f));
            Label(new Rect(225, 52, 830, 22), Mathf.CeilToInt(sim.Boss.Hp) + "  /  " + sim.Boss.MaxHp, 12, Color.white, false, TextAnchor.MiddleCenter);
            Bar(new Rect(225, 80, 830, 4), Mathf.Clamp01(sim.Boss.Stagger / 100), gold);
            Label(new Rect(225, 92, 830, 25), sim.Boss.StaggerTime > 0 ? "무력화! 지금 공격하세요" : PatternHint(sim.Boss.PatternName), 15, sim.Boss.StaggerTime > 0 ? cyan : gold);
            float left = Mathf.Max(0, sim.TimeLimit - sim.Elapsed);
            Label(new Rect(1090, 27, 150, 45), ((int)left / 60).ToString("00") + ":" + ((int)left % 60).ToString("00"), 32, Color.white, true, TextAnchor.MiddleRight);
            Label(new Rect(1055, 89, 183, 24), showBoxes ? "F2 판정 범위 표시 중" : "F1 도움말  ·  F2 범위", 12, new Color(.65f, .7f, .75f), false, TextAnchor.MiddleRight);
            Fill(new Rect(0, 704, 1280, 96), ink);
            int localId = net.Role == NetworkRole.Client ? 1 : 0;
            PlayerState local = sim.Players[Mathf.Min(localId, sim.Players.Count - 1)];
            Label(new Rect(28, 716, 180, 24), (localId + 1) + "P  검의 도전자", 17, cyan, true);
            Bar(new Rect(28, 747, 235, 12), local.Hp / local.MaxHp, cyan);
            Label(new Rect(28, 764, 250, 22), "HP " + Mathf.CeilToInt(local.Hp) + " / " + local.MaxHp + (sim.PracticeInvulnerable ? "  ·  연습 무적" : ""), 12, Color.gray);
            string[] keys = { "J", "Q", "E", "SPACE", "R" };
            string[] names = { "연속 베기", "귀참", "검기 방출", "회피", "각성 · 파천" };
            for (int i = 0; i < 5; i++)
            {
                Rect box = new Rect(306 + i * 126, 716, 116, 68);
                bool ready = local.Cooldowns[i] <= 0;
                Fill(box, new Color(.09f, .11f, .15f)); Outline(box, ready ? (i == 4 ? gold : new Color(.28f, .38f, .45f)) : new Color(.17f, .2f, .25f), 1);
                Label(new Rect(box.x + 9, box.y + 4, 95, 25), ready ? keys[i] : local.Cooldowns[i].ToString("0.0") + "s", 18, ready ? gold : Color.gray, true);
                Label(new Rect(box.x + 9, box.y + 36, 102, 22), names[i], 14, ready ? Color.white : Color.gray);
            }
            Label(new Rect(980, 717, 270, 24), net.Role == NetworkRole.Offline ? "SOLO  /  60 Hz" : (net.Role == NetworkRole.Host ? "HOST" : "CLIENT") + "  /  " + net.PingMs.ToString("0") + " ms", 14, net.Connected || net.Role == NetworkRole.Offline ? cyan : gold, true);
            if (sim.Players.Count > 1)
            {
                PlayerState other = sim.Players[1 - localId];
                Bar(new Rect(980, 749, 250, 7), other.Hp / other.MaxHp, new Color(.65f, .5f, 1));
                Label(new Rect(980, 761, 255, 22), (other.Id + 1) + "P  " + (other.Hp <= 0 ? "전투 불능" : "HP " + Mathf.CeilToInt(other.Hp)), 12, Color.gray);
            }
            else Label(new Rect(980, 750, 255, 26), "K / C 점프  ·  ESC 메뉴", 13, Color.gray);
        }

        static string PatternHint(string name)
        {
            if (name == null) return "";
            if (name.Contains("충격파")) return "보라색 지면 공격! K / C로 점프해서 피하세요";
            if (name.Contains("문양") || name.Contains("룬")) return "푸른 원 안으로 이동하세요";
            if (name.Contains("운석") || name.Contains("낙인")) return "발밑의 표식에서 벗어나세요";
            if (name.Contains("투기장")) return "세 번의 공격을 피하고 반격하세요";
            if (name.Contains("맞이")) return "화염이 없는 길을 찾으세요";
            return "황금색은 공격 예고, 붉은색은 피격 구간입니다";
        }

        void DrawEnd()
        {
            Panel(new Rect(380, 260, 520, 218));
            bool won = sim.Mode == EncounterMode.Victory;
            Label(new Rect(400, 284, 480, 55), won ? "성전의 승리" : "도전 종료", 38, won ? gold : Color.white, true, TextAnchor.MiddleCenter);
            Label(new Rect(400, 350, 480, 28), "전투 시간 " + sim.Elapsed.ToString("0.0") + "초", 17, Color.gray, false, TextAnchor.MiddleCenter);
            if (net.Role != NetworkRole.Client)
            {
                if (Button(new Rect(430, 409, 200, 43), "다시 도전", true)) { sim.Start(); playedEvents.Clear(); animations.Clear(); priorPositions.Clear(); walkingUntil.Clear(); displayPositions.Clear(); displayHeights.Clear(); }
            }
            else Label(new Rect(410, 409, 240, 43), "호스트의 재도전을 기다립니다", 14, cyan);
            if (Button(new Rect(650, 409, 200, 43), "로비로")) ReturnMenu();
        }

        void DrawPause()
        {
            Panel(new Rect(420, 240, 440, 260));
            Label(new Rect(440, 260, 400, 48), "전투 메뉴", 30, Color.white, true, TextAnchor.MiddleCenter);
            Label(new Rect(440, 310, 400, 30), net.Role == NetworkRole.Offline ? "일시 정지" : "협동 전투는 계속 진행됩니다", 15, gold, false, TextAnchor.MiddleCenter);
            if (Button(new Rect(480, 355, 320, 45), "계속하기", true)) pauseMenu = false;
            if (Button(new Rect(480, 419, 320, 45), "로비로 돌아가기")) ReturnMenu();
        }

        void DrawHelp()
        {
            Panel(new Rect(240, 166, 800, 474));
            Label(new Rect(280, 193, 720, 50), "성전 공략 노트", 32, Color.white, true);
            Label(new Rect(280, 259, 720, 255), "WASD / 방향키     이동   ·   J 길게 누르기: 연속 베기\nQ 귀참   ·   E 검기 방출   ·   R 각성\nSPACE 회피 — 짧은 무적   ·   K / C 점프\n\n보라색 충격파는 점프로 피할 수 있습니다.\n운석과 폭발은 공중에서도 맞으니 범위 밖으로 이동하세요.\n룬 패턴의 푸른 원은 안전 지대입니다.\n공격을 누적하면 무력화되며, 보스 체력이 절반이면 2페이즈로 전환합니다.", 18, new Color(.82f, .85f, .9f));
            Label(new Rect(280, 533, 720, 33), "F2 판정 범위   ·   F12 스크린샷   ·   협동: 호스트 IP + UDP 포트로 참가", 14, gold);
            if (Button(new Rect(500, 580, 280, 40), "전투 준비 완료", true)) showHelp = false;
        }

        void ReturnMenu() { if (net != null) net.Dispose(); net = null; sim = null; inGame = false; pauseMenu = false; showHelp = false; }
        void OnDestroy() { Application.logMessageReceived -= OnLog; if (net != null) net.Dispose(); }

        float AnimationTime(int id, string clip, float actionTime)
        {
            float clock = sim != null && !idleReview && sim.Mode == EncounterMode.Fighting ? sim.Elapsed : Time.unscaledTime;
            ActorAnimation state;
            if (!animations.TryGetValue(id, out state)) { state = new ActorAnimation(); animations[id] = state; }
            if (state.Clip != clip || actionTime > state.ActionTime + .03f) { state.Started = clock; state.Clip = clip; }
            state.ActionTime = actionTime;
            return clock - state.Started;
        }

        float WalkClock { get { return sim != null && !idleReview ? sim.Elapsed : Time.unscaledTime; } }

        float PresentationExtrapolation()
        { return net != null && net.Role == NetworkRole.Client && net.Connected ? Mathf.Min(.05f, Time.unscaledTime - snapshotSeenAt) : 0; }

        float ClipDuration(string actor, string name)
        {
            Art entry; if (!art.TryGetValue(actor, out entry)) return 1;
            foreach (Clip clip in entry.clips) if (clip.name == name)
            {
                if (clip.durations != null && clip.durations.Length == clip.frames.Length)
                { float sum = 0; foreach (float duration in clip.durations) sum += duration; return sum; }
                return clip.frames.Length / clip.fps;
            }
            return 1;
        }

        void Sprite(string id, string clipName, Vector2 foot, float scale, int facing, float time, Color color, bool oneShot = false)
        {
            Art entry; if (!art.TryGetValue(id, out entry)) return;
            Clip clip = entry.clips[0]; foreach (Clip candidate in entry.clips) if (candidate.name == clipName) { clip = candidate; break; }
            int index = RaidMotion.Frame(clip.frames, clip.fps, clip.durations, time, oneShot);
            string key = entry.resourcePrefix + index.ToString("D4");
            Texture2D texture;
            if (!textures.TryGetValue(key, out texture)) { texture = Resources.Load<Texture2D>(key); textures[key] = texture; }
            if (texture == null) return;
            Matrix4x4 saved = GUI.matrix;
            if (facing < 0) GUIUtility.ScaleAroundPivot(new Vector2(-1, 1), foot);
            TintTexture(texture, new Rect(foot.x - entry.footX * scale, foot.y - entry.footY * scale, entry.canvasWidth * scale, entry.canvasHeight * scale), color);
            GUI.matrix = saved;
        }

        GUIStyle Style(int size, Color color, bool bold = false, TextAnchor align = TextAnchor.UpperLeft, bool input = false)
        {
            string key = size + ":" + bold + ":" + align + ":" + input;
            GUIStyle style;
            if (!styles.TryGetValue(key, out style))
            {
                style = new GUIStyle(input ? GUI.skin.textField : GUI.skin.label) { font = font, fontSize = size,
                    fontStyle = bold ? FontStyle.Bold : FontStyle.Normal, alignment = align, wordWrap = false,
                    padding = input ? new RectOffset(10, 7, 3, 3) : new RectOffset(0, 0, 0, 0) };
                styles[key] = style;
            }
            style.normal.textColor = color; style.hover.textColor = color; style.focused.textColor = color; return style;
        }
        void Label(Rect rect, string text, int size, Color color, bool bold = false, TextAnchor align = TextAnchor.UpperLeft) { GUI.Label(rect, text, Style(size, color, bold, align)); }
        bool Button(Rect rect, string text, bool accent = false)
        {
            bool hover = rect.Contains(Event.current.mousePosition);
            Fill(rect, accent ? new Color(.72f, .30f, .09f, hover ? 1 : .85f) : new Color(.09f, .12f, .17f, hover ? 1 : .94f));
            Outline(rect, accent ? gold : new Color(.28f, .35f, .42f), 1);
            return GUI.Button(rect, text, Style(17, Color.white, true, TextAnchor.MiddleCenter));
        }
        void Panel(Rect rect) { Fill(new Rect(0, 0, 1280, 800), new Color(0, 0, 0, .6f)); Fill(rect, ink); Outline(rect, new Color(.4f, .3f, .2f), 1); }
        static Color WithAlpha(Color c, float alpha) { c.a = alpha; return c; }
        static void TintTexture(Texture2D tex, Rect rect, Color c) { GUI.color = c; GUI.DrawTexture(rect, tex); GUI.color = Color.white; }
        static void Fill(Rect rect, Color c) { TintTexture(Texture2D.whiteTexture, rect, c); }
        static void Outline(Rect r, Color c, float width)
        { Fill(new Rect(r.x, r.y, r.width, width), c); Fill(new Rect(r.x, r.yMax - width, r.width, width), c); Fill(new Rect(r.x, r.y, width, r.height), c); Fill(new Rect(r.xMax - width, r.y, width, r.height), c); }
        static void Bar(Rect rect, float fraction, Color color) { Fill(rect, new Color(.12f, .12f, .15f)); Fill(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(fraction), rect.height), color); }
        static Texture2D Radial(int size, float inner, bool soft)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false); var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float d = new Vector2((x + .5f) / size * 2 - 1, (y + .5f) / size * 2 - 1).magnitude;
                float alpha = Mathf.Clamp01((1 - d) * size * .35f) * (inner <= 0 ? 1 : Mathf.Clamp01((d - inner) * size * .35f));
                if (soft) alpha *= Mathf.Pow(Mathf.Clamp01(1 - d), 2);
                pixels[y * size + x] = new Color(1, 1, 1, alpha);
            }
            texture.SetPixels(pixels); texture.Apply(); return texture;
        }
        static Texture2D MakeVignette()
        {
            var texture = new Texture2D(128, 80, TextureFormat.RGBA32, false); var pixels = new Color[128 * 80];
            for (int y = 0; y < 80; y++) for (int x = 0; x < 128; x++)
            { float dx = (x - 63.5f) / 64, dy = (y - 39.5f) / 40; pixels[y * 128 + x] = new Color(.01f, .015f, .025f, Mathf.Clamp01((dx * dx + dy * dy) * .42f)); }
            texture.SetPixels(pixels); texture.Apply(); return texture;
        }
        static AudioClip Tone(float frequency, float length, float noise)
        {
            int rate = 22050; float[] data = new float[(int)(rate * length)]; var random = new System.Random(42);
            for (int i = 0; i < data.Length; i++) { float t = i / (float)rate; data[i] = ((float)Math.Sin(t * frequency * (1 - t / length * .4f) * Math.PI * 2) * (1 - noise) + ((float)random.NextDouble() * 2 - 1) * noise) * Mathf.Pow(1 - t / length, 2) * .6f; }
            AudioClip clip = AudioClip.Create("Demo synthesized cue", data.Length, 1, rate, false); clip.SetData(data, 0); return clip;
        }
    }
}
