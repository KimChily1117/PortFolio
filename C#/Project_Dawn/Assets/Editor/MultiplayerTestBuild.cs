using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class MultiplayerTestBuild
{
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
    static void PerformWin64Build(int playerCount)
    {
        EditorUserBuildSettings.SwitchActiveBuildTarget(
            BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows);

        for (int i = 1; i <= playerCount; i++)
        {
            BuildPipeline.BuildPlayer(GetScenePaths(),
                "Builds/Win64/" + GetProjectName() + i.ToString() + "/" + GetProjectName() + i.ToString() + ".exe",
                BuildTarget.StandaloneWindows64, BuildOptions.AutoRunPlayer);
        }
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
            scenes[i] = EditorBuildSettings.scenes[i].path;        }


        return scenes;
    }
}
