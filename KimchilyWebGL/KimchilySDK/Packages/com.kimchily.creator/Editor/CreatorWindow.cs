using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[assembly: InternalsVisibleTo("Kimchily.Creator.Editor.Tests")]
[assembly: InternalsVisibleTo("Kimchily.CompileTests")]

namespace Kimchily.Creator.Editor
{
    public sealed class CreatorWindow : EditorWindow
    {
        [SerializeField] string worldId = "sample-world";
        [SerializeField] SceneAsset entryScene;
        [SerializeField] UnityEngine.Object[] additionalAssets = Array.Empty<UnityEngine.Object>();
        [SerializeField] BuildTarget target = BuildTarget.WebGL;
        [SerializeField] string outputRoot = "WorldBuilds";
        [SerializeField] string lastOutput;
        [SerializeField] string publisherUrl = "http://127.0.0.1:8788";
        [NonSerialized] string publisherToken; // Never serialized, including Editor hot reload.
        [NonSerialized] string localTokenOrigin;
        [SerializeField] string localPublisherDirectory;
        LocalPublisherController localPublisher;
        bool connectLocalWhenReady;
        WorldPublisherClient publisher;
        WorldValidationReport report;
        [SerializeField] Vector2 scroll;
        [SerializeField] string failure;
        [SerializeField] string actionStatus;
        CreatorWindowActionQueue actions;
        bool windowActive;

        enum RequestKind { Validate, Build, BuildAndPublish, PublishLastBuild }
        bool IsBusy => actions?.IsBusy == true || publisher?.Busy == true || localPublisher?.Busy == true;

        [MenuItem("Kimchily/Creator SDK")]
        public static void Open() => GetWindow<CreatorWindow>("Kimchily Web Creator");

        [MenuItem("Kimchily/Publish World")]
        public static void OpenPublisher() => Open();

        [MenuItem("Kimchily/Local Publish Server")]
        public static void OpenLocalServer() => Open();

        void OnEnable()
        {
            windowActive = true;
            actions = new CreatorWindowActionQueue(
                callback => EditorApplication.delayCall += callback.Invoke,
                callback => EditorApplication.delayCall -= callback.Invoke,
                RepaintIfActive, error => { failure = error.Message; actionStatus = "Action failed."; Debug.LogException(error); });
            localPublisher = new LocalPublisherController();
            localPublisher.Changed += LocalPublisherChanged;
            if (string.IsNullOrEmpty(localPublisherDirectory))
                localPublisherDirectory = LocalPublisherController.FindPublisherDirectory(Path.Combine(Application.dataPath, ".."));
            if (File.Exists(Path.Combine(localPublisherDirectory ?? "", "tools", "status.ps1")))
                RunLocalPublisher("status", IsLoopbackPublisher());
            // Preserve the selected platform across Editor reloads. Only a new
            // window starts with the project's current platform.
            if (target == BuildTarget.NoTarget) target = EditorUserBuildSettings.activeBuildTarget;
            if (entryScene == null && !string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
                entryScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SceneManager.GetActiveScene().path);
        }

        void OnDisable()
        {
            if (localPublisher?.Busy == true)
                actionStatus = "The server command continues in the background. Refresh its status when reopening this window.";
            else if (IsBusy) actionStatus = "The window action was cancelled. The last completed build is still available.";
            windowActive = false;
            actions?.Dispose();
            actions = null;
            if (publisher != null) publisher.Changed -= RepaintIfActive;
            publisher?.Dispose();
            publisher = null;
            publisherToken = null;
            localTokenOrigin = null;
            localPublisher?.Dispose();
            localPublisher = null;
        }

        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("World Content", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(IsBusy))
            {
            worldId = EditorGUILayout.TextField("World ID", worldId);
            entryScene = (SceneAsset)EditorGUILayout.ObjectField("Entry Scene", entryScene, typeof(SceneAsset), false);
            target = (BuildTarget)EditorGUILayout.EnumPopup("Build Target", target);
            outputRoot = EditorGUILayout.TextField("Output Folder", outputRoot);
            using (var serialized = new SerializedObject(this))
            {
                EditorGUILayout.PropertyField(serialized.FindProperty("additionalAssets"), true);
                serialized.ApplyModifiedProperties();
            }
            }
            EditorGUILayout.HelpBox("Scene dependencies include imported FBX models, nested prefabs, materials, textures and animations. Declare dynamically loaded assets above. Target-platform compatibility requires device testing.", MessageType.Info);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || IsBusy))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Validate")) QueueRequest(RequestKind.Validate);
                if (GUILayout.Button("Build Content")) QueueRequest(RequestKind.Build);
            }
            DrawPublishing();
            if (!string.IsNullOrEmpty(actionStatus)) EditorGUILayout.LabelField(actionStatus, EditorStyles.wordWrappedLabel);
            if (!string.IsNullOrEmpty(failure)) EditorGUILayout.HelpBox(failure, MessageType.Error);
            if (report != null)
            {
                EditorGUILayout.LabelField("Dependencies", report.Dependencies.Length.ToString());
                EditorGUILayout.LabelField("Bundled Assets", report.BundleAssets.Length.ToString());
                foreach (string error in report.Errors) EditorGUILayout.HelpBox(error, MessageType.Error);
                if (report.RequiredTypes.Any(type => !WorldContentBuilder.IsPortableType(type)))
                {
                    EditorGUILayout.HelpBox("플레이어 모델에 포함된 외부 C# 때문에 막혔다면 게시용 복사본을 만드세요. " +
                        "모델·재질·Animator와 원본은 유지하고, 앱에 없는 스크립트와 그 기능(표정 UI·물리·IK 등)은 복사본에서 제외합니다.", MessageType.Info);
                    using (new EditorGUI.DisabledScope(IsBusy || EditorApplication.isPlayingOrWillChangePlaymode))
                        if (GUILayout.Button("플레이어 모델 게시용 복사본 만들기")) QueuePrepareModels();
                }
                foreach (string warning in report.Warnings) EditorGUILayout.HelpBox(warning, MessageType.Warning);
                foreach (string path in report.Dependencies) EditorGUILayout.SelectableLabel(path, GUILayout.Height(16));
            }
            if (!string.IsNullOrEmpty(lastOutput))
            {
                EditorGUILayout.TextField("Last Build", lastOutput);
                if (GUILayout.Button("Show Build Folder")) EditorUtility.RevealInFinder(lastOutput);
            }
            EditorGUILayout.EndScrollView();
        }

        void DrawPublishing()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Publish to Web / Android", EditorStyles.boldLabel);
            DrawLocalPublisher();
            using (new EditorGUI.DisabledScope(IsBusy))
            {
                string editedUrl = EditorGUILayout.TextField("Server URL", publisherUrl);
                if (editedUrl != publisherUrl)
                {
                    publisherUrl = editedUrl;
                    publisherToken = null;
                    localTokenOrigin = null;
                }
                EditorGUI.BeginChangeCheck();
                string editedToken = EditorGUILayout.PasswordField("Publisher Token", publisherToken ?? string.Empty);
                if (EditorGUI.EndChangeCheck()) { publisherToken = editedToken; localTokenOrigin = null; }
            }
            EditorGUILayout.HelpBox("WebGL은 QR을 기본 카메라로 촬영해 브라우저에서 입장합니다. 서버 켜기는 복제본 서버(8788)에 연결합니다. Built-in 월드를 게시하고 다운로드할 기능은 TypeScript로 작성하세요. Web 실행기는 최초 게시 전에 별도로 빌드해야 합니다.", MessageType.Info);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || IsBusy ||
                                               string.IsNullOrWhiteSpace(publisherUrl) || string.IsNullOrWhiteSpace(publisherToken)))
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!WorldContentBuilder.IsPublishTarget(target) || EditorUserBuildSettings.activeBuildTarget != target))
                {
                    if (GUILayout.Button("Build & Publish")) QueueRequest(RequestKind.BuildAndPublish);
                }
                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(lastOutput) || !File.Exists(Path.Combine(lastOutput, "world.json"))))
                {
                    if (GUILayout.Button("Publish Last Build")) QueueRequest(RequestKind.PublishLastBuild);
                }
            }
            if (!WorldContentBuilder.IsPublishTarget(target) || EditorUserBuildSettings.activeBuildTarget != target)
                EditorGUILayout.HelpBox("Build Profiles에서 Web 또는 Android로 전환하고 위 Build Target도 같은 대상으로 선택하세요.", MessageType.Warning);
            if (publisher == null) return;
            if (!string.IsNullOrEmpty(publisher.Status)) EditorGUILayout.LabelField(publisher.Status, EditorStyles.wordWrappedLabel);
            if (publisher.Busy)
            {
                Rect progress = GUILayoutUtility.GetRect(0, 20, GUILayout.ExpandWidth(true));
                EditorGUI.ProgressBar(progress, publisher.Progress, "Publish progress");
                using (new EditorGUI.DisabledScope(actions?.IsBusy == true))
                {
                    if (GUILayout.Button("Cancel Upload")) QueueCancelUpload();
                }
                return;
            }
            if (!string.IsNullOrEmpty(publisher.Error)) EditorGUILayout.HelpBox(publisher.Error, MessageType.Error);
            if (!string.IsNullOrEmpty(publisher.QrWarning)) EditorGUILayout.HelpBox(publisher.QrWarning, MessageType.Warning);
            if (publisher.Result == null) return;
            EditorGUILayout.TextField("Published URL", publisher.Result.publishUrl);
            EditorGUILayout.TextField("Launch Link", publisher.Result.launchUrl);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Open Published Page")) Application.OpenURL(publisher.Result.publishUrl);
                if (GUILayout.Button("Copy Launch Link")) EditorGUIUtility.systemCopyBuffer = publisher.Result.launchUrl;
            }
            if (publisher.QrTexture != null)
            {
                Rect image = GUILayoutUtility.GetRect(256, 256, GUILayout.ExpandWidth(false));
                GUI.DrawTexture(image, publisher.QrTexture, ScaleMode.ScaleToFit);
            }
        }

        void DrawLocalPublisher()
        {
            if (localPublisher == null) return;
            EditorGUILayout.LabelField("로컬 게시 서버", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(IsBusy))
            {
                if (string.IsNullOrEmpty(localPublisherDirectory))
                    EditorGUILayout.HelpBox("KimchilyPublish 폴더를 선택하면 주소와 토큰을 자동으로 연결합니다.", MessageType.Info);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("서버 켜기 · 연결")) RunLocalPublisher("start", true);
                    if (GUILayout.Button("상태 확인")) RunLocalPublisher("status", IsLoopbackPublisher());
                    using (new EditorGUI.DisabledScope(localPublisher.Status?.processVerified != true))
                        if (GUILayout.Button("서버 끄기")) RunLocalPublisher("stop", false);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("서버 폴더 선택", GUILayout.Width(130)))
                    {
                        string selected = EditorUtility.OpenFolderPanel("KimchilyPublish 폴더", localPublisherDirectory, "");
                        if (!string.IsNullOrEmpty(selected))
                        {
                            localPublisherDirectory = selected;
                            RunLocalPublisher("status", false);
                        }
                    }
                    EditorGUILayout.LabelField(localPublisherDirectory ?? "", EditorStyles.miniLabel);
                }
            }
            if (localPublisher.Busy) EditorGUILayout.LabelField("서버 상태를 확인하고 있습니다...");
            LocalPublisherStatus state = localPublisher.Status;
            if (state != null)
            {
                EditorGUILayout.HelpBox(state.message ?? state.status, state.IsReady ? MessageType.Info : MessageType.Warning);
                if (!string.IsNullOrEmpty(state.publicUrl)) EditorGUILayout.TextField("휴대폰 접속 주소", state.publicUrl);
                if (state.IsReady && !IsBusy)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("이 서버로 연결")) ConnectLocalPublisher();
                        if (GUILayout.Button("휴대폰 주소 복사")) EditorGUIUtility.systemCopyBuffer = state.publicUrl;
                    }
                }
            }
            if (!string.IsNullOrEmpty(localPublisher.Error)) EditorGUILayout.HelpBox(localPublisher.Error, MessageType.Error);
            EditorGUILayout.LabelField("PC와 휴대폰은 같은 Wi-Fi를 사용하세요. 서버를 끄면 QR 입장이 중단되고, 게시 파일은 유지됩니다.", EditorStyles.wordWrappedMiniLabel);
        }

        bool IsLoopbackPublisher() => string.IsNullOrWhiteSpace(publisherUrl) ||
            Uri.TryCreate(publisherUrl, UriKind.Absolute, out Uri value) && value.IsLoopback;

        void RunLocalPublisher(string command, bool connect)
        {
            if (localPublisher == null || localPublisher.Busy) return;
            connectLocalWhenReady = connect;
            try { localPublisher.Run(localPublisherDirectory, command); }
            catch (Exception exception) { failure = exception.Message; }
        }

        void LocalPublisherChanged()
        {
            if (!windowActive || localPublisher == null) return;
            if (!localPublisher.Busy)
            {
                if (connectLocalWhenReady && localPublisher.Status?.IsReady == true) ConnectLocalPublisher();
                if (localPublisher.Status?.status == "stopped" && IsLoopbackPublisher()) publisherToken = null;
                connectLocalWhenReady = false;
            }
            RepaintIfActive();
        }

        void ConnectLocalPublisher()
        {
            try
            {
                string token = LocalPublisherController.ReadLocalToken(localPublisherDirectory, localPublisher.Status);
                publisherUrl = localPublisher.Status.localUrl;
                publisherToken = token;
                localTokenOrigin = publisherUrl;
                failure = null;
                actionStatus = "로컬 서버 연결 완료. 씬을 저장한 뒤 Build & Publish를 누르세요.";
            }
            catch (Exception exception) { failure = exception.Message; }
        }

        void RepaintIfActive()
        {
            if (windowActive && this != null) Repaint();
        }

        void QueueRequest(RequestKind kind)
        {
            if (!windowActive || actions == null || IsBusy || EditorApplication.isPlayingOrWillChangePlaymode) return;
            // Snapshot the request now; the delayed operation must use the
            // settings shown when the button was pressed.
            var request = new WorldBuildRequest
            {
                worldId = worldId,
                entryScene = AssetDatabase.GetAssetPath(entryScene),
                scenes = entryScene == null ? Array.Empty<string>() : new[] { AssetDatabase.GetAssetPath(entryScene) },
                additionalAssets = (additionalAssets ?? Array.Empty<UnityEngine.Object>()).Where(x => x != null)
                    .Select(AssetDatabase.GetAssetPath).ToArray(),
                outputRoot = outputRoot, target = target, requirePortableScripts = kind == RequestKind.BuildAndPublish
            };
            string buildDirectory = lastOutput;
            string serverUrl = publisherUrl;
            string token = publisherToken; // Captured only in this nonserialized queued action.
            string tokenOrigin = localTokenOrigin;
            if (!actions.TryEnqueue(() =>
            {
                if (!windowActive || this == null) return;
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    actionStatus = "Stop Play Mode before building or publishing.";
                    return;
                }
                if (kind == RequestKind.BuildAndPublish || kind == RequestKind.PublishLastBuild)
                    LocalPublisherController.ValidateCredentialDestination(tokenOrigin, serverUrl);
                if (kind == RequestKind.PublishLastBuild) BeginPublish(buildDirectory, serverUrl, token, tokenOrigin);
                else if (Run(request, kind != RequestKind.Validate) && kind == RequestKind.BuildAndPublish && windowActive && this != null)
                    BeginPublish(lastOutput, serverUrl, token, tokenOrigin);
            })) return;
            failure = null;
            actionStatus = kind == RequestKind.Validate ? "Validation queued..." :
                kind == RequestKind.PublishLastBuild ? "Publication queued..." : "Content build queued...";
            RepaintIfActive();
        }

        void QueueCancelUpload()
        {
            var current = publisher;
            if (!windowActive || actions == null || current == null) return;
            actions.TryEnqueue(() =>
            {
                if (!windowActive || publisher != current) return;
                current.Changed -= RepaintIfActive;
                current.Dispose();
                publisher = null;
                actionStatus = "Upload cancelled.";
            });
        }

        void QueuePrepareModels()
        {
            if (!windowActive || actions == null || IsBusy) return;
            string scenePath = AssetDatabase.GetAssetPath(entryScene);
            actions.TryEnqueue(() =>
            {
                string[] prepared = MobilePlayerModelPreparation.PrepareScene(SceneManager.GetSceneByPath(scenePath));
                failure = null;
                report = null;
                actionStatus = prepared.Length == 0
                    ? "변환할 플레이어 모델이 없습니다. 오류가 난 오브젝트의 C# 스크립트를 확인하세요."
                    : "게시용 모델 " + prepared.Length + "개 연결 완료. 씬을 저장한 뒤 Build & Publish를 누르세요.";
            });
        }

        void BeginPublish(string buildDirectory, string serverUrl, string token, string tokenOrigin)
        {
            failure = null;
            if (publisher != null) publisher.Changed -= RepaintIfActive;
            publisher?.Dispose();
            publisher = new WorldPublisherClient();
            publisher.Changed += RepaintIfActive;
            try
            {
                LocalPublisherController.ValidateCredentialDestination(tokenOrigin, serverUrl);
                publisher.Begin(buildDirectory, serverUrl, token);
                actionStatus = null;
            }
            catch (Exception error) { failure = error.Message; actionStatus = "Publication could not start. The completed build is still available."; }
        }

        bool Run(WorldBuildRequest request, bool build)
        {
            failure = null;
            actionStatus = build ? "Building content..." : "Validating content...";
            try
            {
                report = WorldContentBuilder.Validate(request);
                if (build && report.IsValid)
                {
                    EditorUtility.DisplayProgressBar("Kimchily Creator", "Building scene and referenced assets...", 0.5f);
                    var result = WorldContentBuilder.Build(request);
                    lastOutput = result.Directory;
                    actionStatus = "Content build completed.";
                    Debug.Log("Kimchily content built: " + lastOutput);
                    return true;
                }
                actionStatus = report.IsValid ? "Validation completed." : "Validation failed. Review the errors below.";
            }
            catch (Exception ex) { failure = ex.Message; actionStatus = "Content action failed."; Debug.LogException(ex); }
            finally { EditorUtility.ClearProgressBar(); }
            return false;
        }
    }

    /// <summary>A single deferred operation. Scheduling never executes Unity work inside OnGUI.</summary>
    internal sealed class CreatorWindowActionQueue : IDisposable
    {
        readonly Action<Action> schedule;
        readonly Action<Action> unschedule;
        readonly Action changed;
        readonly Action<Exception> failed;
        Action pending;
        bool running;
        bool disposed;

        internal bool IsBusy => pending != null || running;

        internal CreatorWindowActionQueue(Action<Action> schedule, Action<Action> unschedule, Action changed, Action<Exception> failed)
        {
            this.schedule = schedule;
            this.unschedule = unschedule;
            this.changed = changed;
            this.failed = failed;
        }

        internal bool TryEnqueue(Action action)
        {
            if (disposed || IsBusy || action == null) return false;
            pending = action;
            schedule(Execute);
            changed?.Invoke();
            return true;
        }

        void Execute()
        {
            unschedule(Execute);
            if (disposed || pending == null) return;
            Action action = pending;
            pending = null;
            running = true;
            try { changed?.Invoke(); action(); }
            catch (Exception exception) { failed?.Invoke(exception); }
            finally { running = false; if (!disposed) changed?.Invoke(); }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            unschedule(Execute);
            pending = null;
        }
    }
}
