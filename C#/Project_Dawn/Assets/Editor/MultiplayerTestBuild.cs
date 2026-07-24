using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
public class MultiplayerTestBuild
{
    private const int WindowWidth = 800;
    private const int WindowHeight = 600;



    [MenuItem("KIMCHILY_TOOL/Run MultiTest/2Player")]
    static void PerformWin64Build2()
    {
        PerformWin64Build(2);
    }
    [MenuItem("KIMCHILY_TOOL/Run MultiTest/3Player")]
    static void PerformWin64Build3()
    {
        PerformWin64Build(3);
    }
    [MenuItem("KIMCHILY_TOOL/Run MultiTest/4Player")]
    static void PerformWin64Build4()
    {
        PerformWin64Build(4);
    }



    [MenuItem("KIMCHILY_TOOL/Build AOS")]

    static void BuildFromAos()
    {
        EditorUserBuildSettings.SwitchActiveBuildTarget(
           BuildTargetGroup.Android, BuildTarget.Android);

        BuildPipeline.BuildPlayer(GetScenePaths(), "Builds/Android/" + "/" + GetProjectName() + ".apk", BuildTarget.Android, BuildOptions.None);


    }









    //static void PerformWin64Build(int playerCount)
    //{
    //    EditorUserBuildSettings.SwitchActiveBuildTarget(
    //        BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows);

    //    for (int i = 1; i <= playerCount; i++)
    //    {
    //        BuildPipeline.BuildPlayer(GetScenePaths(),
    //            "Builds/Win64/" + GetProjectName() + i.ToString() + "/" + GetProjectName() + i.ToString() + ".exe",
    //            BuildTarget.StandaloneWindows64, BuildOptions.AutoRunPlayer);
    //    }
    //}
    //static string GetProjectName()
    //{
    //    string[] s = Application.dataPath.Split('/');
    //    return s[s.Length - 2];
    //}

    //static string[] GetScenePaths()
    //{
    //    string[] scenes = new string[EditorBuildSettings.scenes.Length];

    //    for (int i = 0; i < scenes.Length; i++)
    //    {
    //        scenes[i] = EditorBuildSettings.scenes[i].path;        }


    //    return scenes;
    //}

    static void PerformWin64Build(int playerCount)
    {
        EditorUserBuildSettings.SwitchActiveBuildTarget(
            BuildTargetGroup.Standalone,
            BuildTarget.StandaloneWindows64);

        // 기존 PlayerSettings 백업
        FullScreenMode prevFullScreenMode = PlayerSettings.fullScreenMode;
        int prevDefaultWidth = PlayerSettings.defaultScreenWidth;
        int prevDefaultHeight = PlayerSettings.defaultScreenHeight;

        try
        {
            // 빌드 기본 실행 설정도 800x600 Windowed로 맞춤
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = WindowWidth;
            PlayerSettings.defaultScreenHeight = WindowHeight;

            for (int i = 1; i <= playerCount; i++)
            {
                string projectName = GetProjectName();
                string clientName = $"{projectName}{i}";
                string buildDir = $"Builds/Win64/{clientName}";
                string exePath = $"{buildDir}/{clientName}.exe";

                BuildPipeline.BuildPlayer(
                    GetScenePaths(),
                    exePath,
                    BuildTarget.StandaloneWindows64,
                    BuildOptions.None);

                RunClient(exePath, i);
            }
        }
        finally
        {
            // Editor 설정 원복
            PlayerSettings.fullScreenMode = prevFullScreenMode;
            PlayerSettings.defaultScreenWidth = prevDefaultWidth;
            PlayerSettings.defaultScreenHeight = prevDefaultHeight;
        }
    }

    static void RunClient(string exePath, int index)
    {
        string fullPath = Path.GetFullPath(exePath);

        if (!File.Exists(fullPath))
        {
            UnityEngine.Debug.LogError($"Build exe not found: {fullPath}");
            return;
        }

        string testClientId = $"build{index:00}";

        string args =
            $"-screen-width {WindowWidth} " +
            $"-screen-height {WindowHeight} " +
            "-screen-fullscreen 0 " +
            $"-testClientId={testClientId}";

        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = fullPath,
            Arguments = args,
            WorkingDirectory = Path.GetDirectoryName(fullPath),
            UseShellExecute = true
        };

        Process.Start(startInfo);

        UnityEngine.Debug.Log($"Run Client: {fullPath} {args}");
    }

    static string GetProjectName()
    {
        string[] s = Application.dataPath.Split('/');
        return s[s.Length - 2];
    }

    static string[] GetScenePaths()
    {
        string[] scenes = new string[EditorBuildSettings.scenes.Length];

        for (int i = 0; i < scenes.Length; i++)
        {
            scenes[i] = EditorBuildSettings.scenes[i].path;
        }

        return scenes;
    }
}
