using Kimchily.Creator.Mobile;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kimchily.Creator.Editor
{
    public static class MobilePlayerMenu
    {
        [MenuItem("Kimchily/World/Add Mobile Player")]
        public static void AddMobilePlayer()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || PrefabStageUtility.GetCurrentPrefabStage() != null) return;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                KimchilyMobilePlayer existing = root.GetComponentInChildren<KimchilyMobilePlayer>(true);
                if (existing == null) continue;
                Selection.activeGameObject = existing.gameObject;
                return;
            }
            KimchilyMobilePlayer player = KimchilyMobilePlayerBootstrap.CreateForScene(scene);
            Undo.RegisterCreatedObjectUndo(player.gameObject, "Add Kimchily Mobile Player");
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = player.gameObject;
        }

        [MenuItem("Kimchily/World/Use Selected Model for Mobile Player")]
        public static void UseSelectedModel()
        {
            GameObject selected = Selection.activeObject as GameObject;
            if (selected == null || !AssetDatabase.Contains(selected)) return;
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || PrefabStageUtility.GetCurrentPrefabStage() != null) return;
            KimchilyMobilePlayer player = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                player = root.GetComponentInChildren<KimchilyMobilePlayer>(true);
                if (player != null) break;
            }
            if (player == null)
            {
                player = KimchilyMobilePlayerBootstrap.CreateForScene(scene, selected);
                Undo.RegisterCreatedObjectUndo(player.gameObject, "Add Kimchily Mobile Player");
            }
            else
            {
                Undo.RecordObject(player, "Set Kimchily Player Model");
                if (player.VisualRoot != null) Undo.DestroyObjectImmediate(player.VisualRoot.gameObject);
                player.ModelPrefab = selected;
                player.RefreshVisual();
                Undo.RegisterCreatedObjectUndo(player.VisualRoot.gameObject, "Set Kimchily Player Model");
            }
            EditorUtility.SetDirty(player);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = player.gameObject;
        }

        [MenuItem("Kimchily/World/Use Selected Model for Mobile Player", true)]
        private static bool CanUseSelectedModel() => Selection.activeObject is GameObject selected && AssetDatabase.Contains(selected);
    }
}
