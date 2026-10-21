using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace DnfRaidDemo
{
    /// <summary>
    /// Opt-in capture of Unity's rendered frame, including IMGUI, for offline video encoding.
    /// Timestamps describe capture requests on the real-time clock; PNG writes are asynchronous.
    /// </summary>
    public sealed class DemoFrameRecorder : MonoBehaviour
    {
        private const double Interval = 1.0 / 30.0;
        [Serializable]
        private sealed class FrameEntry
        {
            public string filename;
            public double timestamp;
            public long utcTicks;
            public int simulationTick;
            public float simulationSeconds;
        }
        [Serializable]
        private sealed class FrameManifest
        {
            public int schemaVersion = 1;
            public string source = "Unity ScreenCapture.CaptureScreenshot; request timestamps";
            public string startedAtUtc;
            public int width, height;
            public int nominalFps = 30;
            public double startDelaySeconds, requestedDurationSeconds, endedAtSeconds;
            public int frameCount, filesPresentAtManifestWrite;
            public bool completed;
            public string stopReason;
            public string role;
            public List<FrameEntry> frames = new List<FrameEntry>();
        }

        private string outputDirectory;
        private double startDelay = 2.0, duration = 20.0;
        private double initializedAt, firstCaptureAt = -1.0, nextCaptureAt;
        private int lastCapturedUnityFrame = -1, originalTargetFrameRate;
        private bool initialized, recordingFinished;
        private RaidDemoApp app;
        private readonly FrameManifest manifest = new FrameManifest();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            string[] args = Environment.GetCommandLineArgs();
            int option = Array.IndexOf(args, "--record-frames");
            if (option < 0) return;
            if (option + 1 >= args.Length || args[option + 1].StartsWith("--", StringComparison.Ordinal))
            {
                Debug.LogError("[DNF_FRAME_RECORD_ERROR] --record-frames requires an output directory.");
                return;
            }
            var owner = new GameObject("Optional rendered frame recorder");
            DontDestroyOnLoad(owner);
            var recorder = owner.AddComponent<DemoFrameRecorder>();
            recorder.outputDirectory = args[option + 1];
            recorder.startDelay = ReadSeconds(args, "--record-start", 2.0, 0.0, 600.0);
            recorder.duration = ReadSeconds(args, "--record-length", 20.0, 0.1, 600.0);
        }

        private static double ReadSeconds(string[] args, string option, double fallback, double minimum, double maximum)
        {
            int index = Array.IndexOf(args, option);
            double value;
            if (index >= 0 && index + 1 < args.Length
                && double.TryParse(args[index + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                && !double.IsNaN(value) && !double.IsInfinity(value) && value >= minimum && value <= maximum)
                return value;
            return fallback;
        }

        private void Start()
        {
            try
            {
                outputDirectory = Path.GetFullPath(outputDirectory);
                Directory.CreateDirectory(outputDirectory);
                if (File.Exists(Path.Combine(outputDirectory, "frames.json"))
                    || Directory.GetFiles(outputDirectory, "frame*.png").Length != 0)
                    throw new IOException("Use a new frame directory to preserve previous recordings.");
                originalTargetFrameRate = Application.targetFrameRate;
                app = FindObjectOfType<RaidDemoApp>();
                Application.targetFrameRate = 30;
                initializedAt = Time.realtimeSinceStartupAsDouble;
                nextCaptureAt = initializedAt + startDelay;
                manifest.width = Screen.width;
                manifest.height = Screen.height;
                manifest.startDelaySeconds = startDelay;
                manifest.requestedDurationSeconds = duration;
                manifest.role = app == null ? "none" : app.CaptureRole;
                initialized = true;
                Debug.Log("[DNF_FRAME_RECORD_READY] path=" + outputDirectory + "; start=" + startDelay + "; length=" + duration);
            }
            catch (Exception error)
            {
                Debug.LogError("[DNF_FRAME_RECORD_ERROR] " + error.Message);
                enabled = false;
            }
        }

        private void Update()
        {
            if (!initialized || recordingFinished) return;
            double now = Time.realtimeSinceStartupAsDouble;
            if (firstCaptureAt >= 0 && now - firstCaptureAt >= duration)
            {
                Finish("duration_reached", now);
                return;
            }
            if (now < nextCaptureAt || Time.frameCount == lastCapturedUnityFrame) return;
            if (firstCaptureAt < 0)
            {
                firstCaptureAt = now;
                manifest.startedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                manifest.width = Screen.width;
                manifest.height = Screen.height;
            }
            string filename = "frame" + manifest.frames.Count.ToString("D6", CultureInfo.InvariantCulture) + ".png";
            try
            {
                ScreenCapture.CaptureScreenshot(Path.Combine(outputDirectory, filename));
                var state = app == null ? null : app.Simulation;
                manifest.frames.Add(new FrameEntry { filename = filename, timestamp = now - firstCaptureAt,
                    utcTicks = DateTime.UtcNow.Ticks, simulationTick = state == null ? 0 : state.TickIndex,
                    simulationSeconds = state == null ? 0 : state.Elapsed });
                lastCapturedUnityFrame = Time.frameCount;
                // Never request catch-up frames: each request corresponds to a different rendered update.
                nextCaptureAt += Interval;
                if (nextCaptureAt <= now) nextCaptureAt = now + Interval;
            }
            catch (Exception error)
            {
                Debug.LogError("[DNF_FRAME_RECORD_ERROR] " + error.Message);
                Finish("capture_error", now);
            }
        }

        private void Finish(string reason, double now)
        {
            recordingFinished = true;
            manifest.completed = reason == "duration_reached";
            manifest.stopReason = reason;
            manifest.endedAtSeconds = firstCaptureAt < 0 ? 0.0 : now - firstCaptureAt;
            Application.targetFrameRate = originalTargetFrameRate;
            SaveManifest();
            Debug.Log("[DNF_FRAME_RECORD_FINISHED] frames=" + manifest.frameCount
                + "; elapsed=" + manifest.endedAtSeconds + "; reason=" + reason);
        }

        private void SaveManifest()
        {
            manifest.frameCount = manifest.frames.Count;
            manifest.filesPresentAtManifestWrite = 0;
            foreach (FrameEntry entry in manifest.frames)
                if (File.Exists(Path.Combine(outputDirectory, entry.filename))) manifest.filesPresentAtManifestWrite++;
            try
            {
                File.WriteAllText(Path.Combine(outputDirectory, "frames.json"), JsonUtility.ToJson(manifest, true));
            }
            catch (Exception error) { Debug.LogError("[DNF_FRAME_RECORD_ERROR] Manifest: " + error.Message); }
        }

        private void OnApplicationQuit()
        {
            if (!initialized) return;
            if (!recordingFinished) Finish("application_quit", Time.realtimeSinceStartupAsDouble);
            else SaveManifest(); // Refresh the count after asynchronous PNG writes had time to finish.
        }
    }
}
