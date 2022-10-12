using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneManangers
{
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
    LoadScene(GetSceneNames((type)));
  }

  private void LoadScene(string scenename)
  {
    SceneManager.LoadScene(scenename);
  }
}
