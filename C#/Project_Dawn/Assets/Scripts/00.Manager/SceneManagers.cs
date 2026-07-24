using DG.Tweening;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneManagers : MonoBehaviour
{
    private const float LoadingSceneMinimumSeconds = 1.2f;

    private Define.Scenes _currentScene = Define.Scenes.NONE;
    public Define.Scenes CurrentScene { get { return _currentScene; } set { _currentScene = value; } }

    private Define.Scenes _nextScene = Define.Scenes.NONE;
    public Define.Scenes NextScene { get { return _nextScene; } set { _nextScene = value; } }

    public BaseScene CurrentActiveScene
    {
        get { return GameObject.FindObjectOfType<BaseScene>(); }
    }

    private string GetSceneNames(Define.Scenes type)
    {
        switch (type)
        {
            case Define.Scenes.LOGIN:
                return "Title(Login)";
            case Define.Scenes.LOBBY:
                return "Character SelectScene";
            case Define.Scenes.TOWN:
                return "Town";
            case Define.Scenes.BAKAL:
                return "Bakal";
            case Define.Scenes.LOADING:
                return "LoadingScene";
            default:
                return Enum.GetName(typeof(Define.Scenes), type);
        }
    }

    public void LoadScene(Define.Scenes type)
    {
        NextScene = type;
        LoadScene(GetSceneNames(type));
    }

    private void LoadScene(string scenename)
    {
        SceneManager.LoadScene(scenename);
    }

    public void LoadSceneAsync(Define.Scenes type, Action cbAction)
    {
        NextScene = type;
        StartCoroutine(LoadSceneThroughLoadingScene(type, cbAction));
    }

    private IEnumerator LoadSceneThroughLoadingScene(Define.Scenes targetScene, Action cbCompleteAction)
    {
        string loadingSceneName = GetSceneNames(Define.Scenes.LOADING);
        string targetSceneName = GetSceneNames(targetScene);

        if (targetScene != Define.Scenes.LOADING && SceneManager.GetActiveScene().name != loadingSceneName)
        {
            SceneLoadingOverlay.SetDetail("로딩 화면을 준비하는 중입니다.");
            yield return LoadUnitySceneAsync(loadingSceneName);
            CurrentScene = Define.Scenes.LOADING;
            yield return new WaitForSecondsRealtime(LoadingSceneMinimumSeconds);
        }

        SceneLoadingOverlay.SetDetail("목적지 씬을 불러오는 중입니다.");
        yield return LoadUnitySceneAsync(targetSceneName);

        cbCompleteAction?.Invoke();
    }

    private IEnumerator LoadUnitySceneAsync(string sceneName)
    {
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        if (operation == null)
        {
            Debug.LogError($"[SCENE] LoadSceneAsync failed to start. Scene={sceneName}");
            yield break;
        }

        operation.allowSceneActivation = true;
        while (!operation.isDone)
            yield return null;
    }

    #region 씬전환에 들어가는 연출 code

    public void SceneTransferLefttoRight()
    {
    }

    public void SceneTransferFadeInout()
    {
        DOTween.Kill("SceneTransferFadeInout");
    }

    #endregion
}


