using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public abstract class BaseScene : MonoBehaviour
{
  private static Define.Scenes _currentScene = Define.Scenes.NONE;
  public static Define.Scenes CurrentScene { get { return _currentScene;} set { } }
    
    // Start is called before the first frame update
    protected virtual void Awake()
    {
      Initialize();
    }

    protected virtual void Initialize()
    {



    }

}
