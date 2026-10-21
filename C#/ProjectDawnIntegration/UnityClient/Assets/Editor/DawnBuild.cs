using DnfRaidDemo;
public static class DawnBuild
{
    public static void Build()
    {
        foreach(var name in new[]{"DawnLogin","DawnTown","DawnLoading"}) {
            var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/"+name+".unity");
            foreach(var root in scene.GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<UnityEngine.Transform>(true))UnityEditor.GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        }
        SimulationChecks.Run();
        MotionChecks.Run();
        FrenzyChecks.Run();
        ThrustChecks.Run();
        DawnAssetChecks.Run();
        MeteorVisualChecks.Run();
        ChatEnterChecks.Run();
        DemoBuild.BuildWindows();
    }
}
