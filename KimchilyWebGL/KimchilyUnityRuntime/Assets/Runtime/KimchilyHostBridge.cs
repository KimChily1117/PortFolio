using System;
using System.Collections.Generic;
using Kimchily.Creator;
using Kimchily.Creator.Content;
using Kimchily.Creator.Mobile;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;

namespace Kimchily.World
{
    /// <summary>
    /// Persistent Unity-as-a-Library endpoint. Native hosts send JSON to Receive
    /// on the KimchilyHostBridge GameObject and wait for the matching request ID.
    /// Loads either the bundled demo or a hash-pinned published world revision.
    /// </summary>
    [DisallowMultipleComponent]
    [Preserve]
    public sealed class KimchilyHostBridge : MonoBehaviour
    {
        public static KimchilyHostBridge Instance { get; private set; }

        // Editor/tests can observe the same events sent to the Android host.
        public event Action<HostEvent> EventRaised;
        public WorldRuntimeState State { get; private set; }
        public string CurrentWorldId => openCommand?.worldId ?? string.Empty;
        public string CurrentRevisionId => openCommand?.revisionId ?? string.Empty;
        public string CurrentOpenRequestId => openCommand?.requestId ?? string.Empty;

        readonly object queueLock = new object();
        readonly Queue<string> commands = new Queue<string>();
        readonly List<HostCommand> closeCommands = new List<HostCommand>();
        HostCommand openCommand;
        Scene bootstrapScene;
        Scene worldScene;
        AsyncOperation operation;
        WorldContentDownload download;
        WorldContentSession content;
        bool openTerminalSent;
        bool closeRequestedFromUI;
        float lastProgress;
        bool awaitingScriptStart;
        float scriptStartDeadline;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            gameObject.name = HostProtocol.BridgeObjectName;
            bootstrapScene = gameObject.scene;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
            State = WorldRuntimeState.Idle;
#if UNITY_ANDROID && !UNITY_EDITOR
            Screen.autorotateToPortrait = true;
            Screen.autorotateToPortraitUpsideDown = true;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;
#endif
        }

        void Start()
        {
            if (Instance == this)
                EmitReady(string.Empty);
        }

        /// <summary>
        /// Enqueue only: all JSON parsing, scene APIs and callbacks run in Update.
        /// This also makes direct managed callers safe if they use a worker thread.
        /// Request IDs must be unique for distinct OpenWorld/CloseWorld operations.
        /// </summary>
        [Preserve]
        public void Receive(string json)
        {
            lock (queueLock) commands.Enqueue(json);
        }

        void Update()
        {
            if (Instance != this) return;
            // Bound work per frame without reordering accepted native messages.
            for (int count = 0; count < 32; count++)
            {
                string json;
                lock (queueLock)
                {
                    if (commands.Count == 0) break;
                    json = commands.Dequeue();
                }
                Process(json);
            }
            AdvanceDownload();
            AdvanceSceneOperation();
            AdvanceScriptInitialization();
        }

        void Process(string json)
        {
            HostCommand command;
            try
            {
                if (string.IsNullOrWhiteSpace(json))
                    throw new ArgumentException("A JSON command is required.");
                command = JsonUtility.FromJson<HostCommand>(json);
                if (command == null)
                    throw new ArgumentException("A JSON object is required.");
            }
            catch (Exception exception)
            {
                Fail(null, "INVALID_JSON", exception.Message);
                return;
            }
            if (command.protocolVersion != HostProtocol.Version)
            {
                Fail(command, "UNSUPPORTED_PROTOCOL", "The runtime requires protocolVersion 1.");
                return;
            }
            if (string.IsNullOrWhiteSpace(command.requestId))
            {
                Fail(command, "INVALID_REQUEST", "A non-empty requestId is required.");
                return;
            }

            switch (command.type)
            {
                case "Initialize":
                    EmitReady(command.requestId);
                    break;
                case "OpenWorld":
                    Open(command);
                    break;
                case "CloseWorld":
                    Close(command);
                    break;
                default:
                    Fail(command, "UNKNOWN_COMMAND", "Use Initialize, OpenWorld or CloseWorld.");
                    break;
            }
        }

        bool ValidateDemo(HostCommand command, bool allowEmptyWorld)
        {
            if (command.worldId != HostProtocol.DemoWorldId &&
                !(allowEmptyWorld && string.IsNullOrEmpty(command.worldId)))
            {
                Fail(command, "UNKNOWN_WORLD", "This runtime contains only the demo world.");
                return false;
            }
            if (!string.IsNullOrEmpty(command.revisionId) && command.revisionId != HostProtocol.DemoRevisionId)
            {
                Fail(command, "UNKNOWN_REVISION", "The bundled demo revision is builtin-v1.");
                return false;
            }
            command.worldId = HostProtocol.DemoWorldId;
            command.revisionId = HostProtocol.DemoRevisionId;
            return true;
        }

        void Open(HostCommand command)
        {
            bool remote = !string.IsNullOrEmpty(command.manifestUrl);
            if (!remote && !ValidateDemo(command, false)) return;
            if (State != WorldRuntimeState.Idle)
            {
                Fail(command, "BUSY", "Close the current world and wait for WorldClosed before opening another.");
                return;
            }
            if (!remote && !Application.CanStreamedLevelBeLoaded(HostProtocol.DemoSceneName))
            {
                Fail(command, "SCENE_UNAVAILABLE", "DemoWorld is missing from the player build.");
                return;
            }

            openCommand = command;
            openTerminalSent = false;
            closeRequestedFromUI = false;
            lastProgress = 0;
            State = WorldRuntimeState.Loading;
            try
            {
                if (remote)
                {
                    download = new WorldContentDownload(command.manifestUrl, command.manifestSha256,
                        command.worldId, command.revisionId);
                    Emit("WorldProgress", command, 0, string.Empty, "Downloading and verifying the published world.");
                    return;
                }
                operation = SceneManager.LoadSceneAsync(HostProtocol.DemoSceneName, LoadSceneMode.Additive);
                if (operation == null) throw new InvalidOperationException("Unity did not start loading DemoWorld.");
                // Do not disable scene activation: Unity cannot cancel a scene load,
                // and deferred activation could prevent a requested unload finishing.
                Emit("WorldProgress", command, 0, string.Empty, "Loading the bundled demo.");
            }
            catch (Exception exception)
            {
                FailLoad(exception.Message);
            }
        }

        void Close(HostCommand command)
        {
            if (openCommand != null)
            {
                if ((!string.IsNullOrEmpty(command.worldId) && command.worldId != openCommand.worldId) ||
                    (!string.IsNullOrEmpty(command.revisionId) && command.revisionId != openCommand.revisionId))
                {
                    Fail(command, "WRONG_WORLD", "CloseWorld must match the current world and revision.");
                    return;
                }
                command.worldId = openCommand.worldId;
                command.revisionId = openCommand.revisionId;
            }
            if (State == WorldRuntimeState.Idle)
            {
                Emit("WorldClosed", command, 1, "ALREADY_CLOSED", "No world is open.");
                return;
            }
            // Coalesce repeated close commands but acknowledge every unique ID.
            if (closeCommands.Exists(item => item.requestId == command.requestId)) return;
            closeCommands.Add(command);
            if (download != null && content == null && operation == null)
            {
                CompleteClose();
                return;
            }
            if (State == WorldRuntimeState.Ready || State == WorldRuntimeState.Faulted)
                BeginUnload();
            // During Loading we finish the non-cancellable Unity load, then unload
            // without emitting WorldReady. During Closing we share that unload.
        }

        void AdvanceDownload()
        {
            if (download == null || content != null || State != WorldRuntimeState.Loading) return;
            download.Tick();
            if (download.Error != null) { FailLoad(download.Error.Message); return; }
            float progress = download.Progress * 0.7f;
            if (progress - lastProgress >= 0.05f)
            {
                lastProgress = progress;
                Emit("WorldProgress", openCommand, progress, string.Empty, "Downloading and verifying the published world.");
            }
            if (!download.IsComplete) return;
            try
            {
                content = WorldContentSession.OpenLocal(download.DirectoryPath);
                operation = content.LoadEntrySceneAsync();
                if (operation == null) throw new InvalidOperationException("Unity did not start loading the published scene.");
            }
            catch (Exception exception) { FailLoad(exception.Message); }
        }

        void AdvanceSceneOperation()
        {
            if (operation == null) return;
            if (State == WorldRuntimeState.Loading)
            {
                if (!operation.isDone)
                {
                    float fraction = Mathf.Clamp01(operation.progress / 0.9f);
                    float progress = content == null ? fraction : 0.7f + fraction * 0.3f;
                    if (progress - lastProgress >= 0.1f)
                    {
                        lastProgress = progress;
                        Emit("WorldProgress", openCommand, progress, string.Empty,
                            closeCommands.Count == 0 ? "Loading the world scene." : "Finishing the load before closing.");
                    }
                    return;
                }
                operation = null;
                worldScene = content == null ? SceneManager.GetSceneByName(HostProtocol.DemoSceneName) :
                    SceneManager.GetSceneByPath(content.Manifest.entryScene);
                if (!worldScene.IsValid() || !worldScene.isLoaded)
                {
                    FailLoad("Unity finished loading but the world scene is not available.");
                    return;
                }
                if (closeCommands.Count > 0)
                {
                    BeginUnload();
                    return;
                }
                SceneManager.SetActiveScene(worldScene);
                awaitingScriptStart = true;
                scriptStartDeadline = Time.realtimeSinceStartup + 10;
            }
            else if (State == WorldRuntimeState.Closing && operation.isDone)
            {
                operation = null;
                if (worldScene.IsValid() && worldScene.isLoaded)
                {
                    FailUnload("Unity finished unloading but the world scene is still loaded.");
                    return;
                }
                CompleteClose();
            }
        }

        void AdvanceScriptInitialization()
        {
            if (!awaitingScriptStart || State != WorldRuntimeState.Loading) return;
            if (closeCommands.Count > 0) { BeginUnload(); return; }
            bool waiting = false;
            string failure = null;
            foreach (GameObject root in worldScene.GetRootGameObjects())
                foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (!(behaviour is IWorldScriptStatus script)) continue;
                    if (script.IsFaulted) { failure = behaviour.name + ": " + script.LastError; break; }
                    if (behaviour.isActiveAndEnabled && !script.HasStarted) waiting = true;
                }
            if (failure == null && waiting && Time.realtimeSinceStartup < scriptStartDeadline) return;
            awaitingScriptStart = false;
            if (failure != null || waiting)
            {
                openTerminalSent = true;
                Fail(openCommand, "SCRIPT_INITIALIZATION_FAILED", failure ?? "World scripts did not finish initialization.");
                BeginUnload();
                return;
            }
            try { KimchilyMobilePlayerBootstrap.EnsureForScene(worldScene); }
            catch (Exception exception)
            {
                openTerminalSent = true;
                Fail(openCommand, "PLAYER_INITIALIZATION_FAILED", exception.Message);
                BeginUnload();
                return;
            }
            State = WorldRuntimeState.Ready;
            openTerminalSent = true;
            Emit("WorldProgress", openCommand, 1, string.Empty, "World loaded.");
            Emit("WorldReady", openCommand, 1, string.Empty, "The world is ready.");
        }

        void BeginUnload()
        {
            awaitingScriptStart = false;
            State = WorldRuntimeState.Closing;
            try
            {
                CancelWorldCoroutines();
                if (!worldScene.IsValid() || !worldScene.isLoaded)
                {
                    CompleteClose();
                    return;
                }
                // Cancel explicit SDK work first, then disable scene behaviours so
                // they cannot schedule new work while Unity unloads their objects.
                foreach (GameObject root in worldScene.GetRootGameObjects()) root.SetActive(false);
                if (bootstrapScene.IsValid() && bootstrapScene.isLoaded)
                    SceneManager.SetActiveScene(bootstrapScene);
                operation = SceneManager.UnloadSceneAsync(worldScene);
                if (operation == null) throw new InvalidOperationException("Unity did not start unloading DemoWorld.");
            }
            catch (Exception exception)
            {
                FailUnload(exception.Message);
            }
        }

        void CancelWorldCoroutines()
        {
            if (!worldScene.IsValid() || !worldScene.isLoaded) return;
            foreach (GameObject root in worldScene.GetRootGameObjects())
                foreach (CoroutineScheduler scheduler in root.GetComponentsInChildren<CoroutineScheduler>(true))
                    scheduler.CancelAll();
        }

        void CompleteClose()
        {
            HostCommand opening = openCommand;
            bool openingWasCancelled = !openTerminalSent;
            HostCommand[] closing = closeCommands.ToArray();
            try { ResetSession(); }
            catch (Exception exception) { FailUnload(exception.Message); return; }
            if (openingWasCancelled && opening != null)
                Fail(opening, "CANCELLED", "The world was closed before it became ready.");
            foreach (HostCommand command in closing)
                Emit("WorldClosed", command, 1, string.Empty, "The world was unloaded and its coroutines were cancelled.");
        }

        void FailLoad(string message)
        {
            HostCommand opening = openCommand;
            HostCommand[] closing = closeCommands.ToArray();
            try { ResetSession(); }
            catch (Exception exception) { FailUnload(exception.Message); return; }
            Fail(opening, "LOAD_FAILED", message);
            foreach (HostCommand command in closing)
                Emit("WorldClosed", command, 1, string.Empty, "No world remains loaded.");
        }

        void FailUnload(string message)
        {
            operation = null;
            State = WorldRuntimeState.Faulted;
            closeRequestedFromUI = false;
            if (!openTerminalSent)
            {
                openTerminalSent = true;
                Fail(openCommand, "UNLOAD_FAILED", message);
            }
            HostCommand[] closing = closeCommands.ToArray();
            closeCommands.Clear();
            foreach (HostCommand command in closing) Fail(command, "UNLOAD_FAILED", message);
            // Retain the scene/session in Faulted so another CloseWorld can retry;
            // never report WorldClosed while its scene may still be loaded.
        }

        void ResetSession()
        {
            content?.Dispose();
            content = null;
            download?.Dispose();
            download = null;
            operation = null;
            worldScene = default;
            openCommand = null;
            closeCommands.Clear();
            openTerminalSent = false;
            closeRequestedFromUI = false;
            awaitingScriptStart = false;
            State = WorldRuntimeState.Idle;
        }

        /// <summary>Optional world UI exit. Android lets native allocate its close request ID.</summary>
        public void RequestCloseFromUI()
        {
            if (openCommand == null || closeRequestedFromUI || State == WorldRuntimeState.Closing) return;
            closeRequestedFromUI = true;
#if UNITY_ANDROID && !UNITY_EDITOR
            Emit("CloseRequested", openCommand, 0, string.Empty, "The world requested a return to the host.");
#else
            Receive(JsonUtility.ToJson(new HostCommand
            {
                protocolVersion = HostProtocol.Version,
                type = "CloseWorld",
                requestId = "unity-close-" + Guid.NewGuid().ToString("N"),
                worldId = CurrentWorldId,
                revisionId = CurrentRevisionId
            }));
#endif
        }

        void EmitReady(string requestId)
        {
            Emit("RuntimeReady", new HostCommand { requestId = requestId }, 1, string.Empty,
                "Kimchily runtime is ready for the bundled demo and published worlds.");
        }

        void Fail(HostCommand command, string code, string message) =>
            Emit("WorldFailed", command, 0, code, message);

        void Emit(string type, HostCommand command, float progress, string code, string message)
        {
            var payload = new HostEvent
            {
                type = type,
                requestId = command?.requestId ?? string.Empty,
                worldId = command?.worldId ?? string.Empty,
                revisionId = command?.revisionId ?? string.Empty,
                progress = progress,
                code = code,
                message = message
            };
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var androidBridge = new AndroidJavaClass("com.kimchily.app.UnityHostBridge"))
                    androidBridge.CallStatic("onUnityEvent", JsonUtility.ToJson(payload));
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Kimchily native callback failed: " + exception.Message);
            }
#endif
            Action<HostEvent> observers = EventRaised;
            if (observers == null) return;
            foreach (Action<HostEvent> observer in observers.GetInvocationList())
            {
                try { observer(payload); }
                catch (Exception exception) { Debug.LogWarning("Kimchily event observer failed: " + exception.Message); }
            }
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            CancelWorldCoroutines();
            if (!worldScene.IsValid() || !worldScene.isLoaded) content?.Dispose();
            download?.Dispose();
            Instance = null;
            lock (queueLock) commands.Clear();
        }
    }
}
