using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class LoginScene : BaseScene
{
    protected override void Initialize()
    {   
        GameManager.Input.Clear(); 
        GameManager.SCENE.CurrentScene = Define.Scenes.LOGIN;

    }

    // Todo : Added Login Scene

    private void Update()
    {
        if (Application.platform == RuntimePlatform.Android)
        {
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);

                if (touch.phase == TouchPhase.Began)
                {
                    GameManager.SCENE.LoadScene(Define.Scenes.TOWN);
                }
            }
        }
        else
        {
            if (Input.GetKeyDown(KeyCode.Return))
            {
                GameManager.SCENE.LoadScene(Define.Scenes.TOWN);
            }
        }
    }

}