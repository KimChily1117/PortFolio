using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Kimchily.Creator.Editor
{
    /// <summary>Runs the exact Creator-window ZIP/UWR/QR path for batch verification.</summary>
    public static class WorldPublisherBatch
    {
        static WorldPublisherClient publisher;
        static string resultFile;
        static double deadline;
        static bool completed;

        // Use -batchmode -executeMethod Kimchily.Creator.Editor.WorldPublisherBatch.Publish
        // without -quit: EditorApplication.update must continue until the HTTP work finishes.
        public static void Publish()
        {
            try
            {
                string directory = Argument("-publishBuildDirectory");
                string server = Argument("-publishServerUrl");
                string tokenFile = Argument("-publishTokenFile");
                resultFile = Path.GetFullPath(Argument("-publishResultFile"));
                string token = File.ReadAllText(tokenFile).Trim();
                completed = false;
                deadline = EditorApplication.timeSinceStartup + 240;
                publisher = new WorldPublisherClient();
                publisher.Changed += OnChanged;
                EditorApplication.update += CheckTimeout;
                publisher.Begin(directory, server, token);
            }
            catch (Exception error) { Finish(1, error.Message); }
        }

        static string Argument(string name)
        {
            string[] values = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(values, name);
            if (index < 0 || index + 1 >= values.Length || string.IsNullOrWhiteSpace(values[index + 1]))
                throw new ArgumentException("Missing batch argument: " + name);
            return values[index + 1];
        }

        static void CheckTimeout()
        {
            if (!completed && EditorApplication.timeSinceStartup >= deadline)
                Finish(1, "Publisher batch verification timed out.");
        }

        static void OnChanged()
        {
            if (completed || publisher == null || publisher.Busy) return;
            if (!string.IsNullOrEmpty(publisher.Error)) { Finish(1, publisher.Error); return; }
            if (publisher.Result == null) { Finish(1, "Publisher returned no result."); return; }
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(resultFile));
                File.WriteAllText(resultFile, JsonUtility.ToJson(publisher.Result, true));
                if (publisher.QrTexture == null)
                {
                    Finish(1, "Published, but QR download/decoding failed: " + publisher.QrWarning);
                    return;
                }
                Debug.Log("Kimchily publication verified: " + publisher.Result.publishUrl +
                          " (QR " + publisher.QrTexture.width + "x" + publisher.QrTexture.height + ")");
                Finish(0, null);
            }
            catch (Exception error) { Finish(1, error.Message); }
        }

        static void Finish(int exitCode, string error)
        {
            if (completed) return;
            completed = true;
            EditorApplication.update -= CheckTimeout;
            if (publisher != null)
            {
                publisher.Changed -= OnChanged;
                publisher.Dispose();
                publisher = null;
            }
            if (!string.IsNullOrEmpty(error)) Debug.LogError("Kimchily publication failed: " + error);
            EditorApplication.Exit(exitCode);
        }
    }
}
