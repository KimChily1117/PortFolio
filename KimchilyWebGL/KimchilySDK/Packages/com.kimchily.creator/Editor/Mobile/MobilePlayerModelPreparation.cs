using System;
using System.Collections.Generic;
using System.IO;
using Kimchily.Creator.Mobile;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kimchily.Creator.Editor
{
    public static class MobilePlayerModelPreparation
    {
        public static string[] PrepareScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("Open the entry scene before preparing player models.");
            var outputs = new List<string>();
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (var player in root.GetComponentsInChildren<KimchilyMobilePlayer>(true))
                    if (player.ModelPrefab != null && PublishableModelUtility.GetUnsupportedScripts(player.ModelPrefab).Length > 0)
                        outputs.Add(PreparePlayer(player));
            return outputs.ToArray();
        }

        public static string PreparePlayer(KimchilyMobilePlayer player)
        {
            if (player == null || EditorApplication.isPlayingOrWillChangePlaymode ||
                AssetDatabase.Contains(player) || PrefabStageUtility.GetCurrentPrefabStage() != null)
                throw new InvalidOperationException("Select a Mobile Player in an open world scene outside Play Mode.");
            GameObject source = player.ModelPrefab;
            if (source == null) throw new InvalidOperationException("Assign a model prefab first.");
            string folder = !player.gameObject.scene.path.StartsWith("Assets/", StringComparison.Ordinal) ? "Assets" :
                Path.GetDirectoryName(player.gameObject.scene.path).Replace('\\', '/');
            folder += "/PublishedModels";
            EnsureFolder(folder);
            string filename = source.name;
            foreach (char invalid in Path.GetInvalidFileNameChars()) filename = filename.Replace(invalid, '_');
            string output = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + filename + "_Publishable.prefab");
            GameObject prepared = PublishableModelUtility.CreateCopy(source, output, out string[] removed);
            Undo.IncrementCurrentGroup();
            int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Prepare Kimchily player model for publishing");
            try
            {
                Undo.RecordObject(player, "Assign publishable model");
                if (player.VisualRoot != null) Undo.DestroyObjectImmediate(player.VisualRoot.gameObject);
                player.ModelPrefab = prepared;
                player.RefreshVisual();
                Undo.RegisterCreatedObjectUndo(player.VisualRoot.gameObject, "Create publishable model preview");
                if (PrefabUtility.IsPartOfPrefabInstance(player)) PrefabUtility.RecordPrefabInstancePropertyModifications(player);
                EditorUtility.SetDirty(player);
                EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
                Undo.CollapseUndoOperations(undo);
                Debug.Log("[Kimchily] Publishable model assigned: " + output +
                    "\nExcluded scripts (source preserved): " + string.Join(", ", removed), player);
                return output;
            }
            catch
            {
                Undo.RevertAllDownToGroup(undo);
                throw;
            }
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        [MenuItem("Kimchily/World/Prepare Mobile Player for Publish")]
        public static void PrepareActiveScene()
        {
            string[] outputs = PrepareScene(SceneManager.GetActiveScene());
            Debug.Log(outputs.Length == 0 ? "[Kimchily] No unsupported player model scripts found." :
                "[Kimchily] Prepared " + outputs.Length + " player model(s). Save the scene, then Build & Publish.");
        }
    }
}
