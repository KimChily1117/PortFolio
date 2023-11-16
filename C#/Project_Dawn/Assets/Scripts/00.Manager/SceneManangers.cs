using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneManangers
{

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
        string Scenename = Enum.GetName(typeof(Define.Scenes), type);
        return Scenename;
    }

    public void LoadScene(Define.Scenes type)
    {
        NextScene = type;
        //TODO : SwitchScene���� �Ѿ�� ȿ�� ���� �߰� �ڵ� �ʿ�
        LoadScene(GetSceneNames((type)));
    }

    private void LoadScene(string scenename)
    {
        SceneManager.LoadScene(scenename);
    }

    #region ����ȯ�� ���� ���� code

    public void SceneTransferLefttoRight()
    {

    }

    public void SceneTransferFadeInout()
    {

    }

    #endregion
}
