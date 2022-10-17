using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class LoginScene : BaseScene
{
    protected override void Initialize()
    {   
        base.Initialize();
        _currentScene = Define.Scenes.LOGIN; 
        UnityEngine.Object obj = FindObjectOfType(typeof(EventSystem));

        if (obj == null)
        {
            GameManager.Resources.Instantiate("ETC/EventSystem").name = "@EventSystem";
        }
    }

    private void Update()
    {
        if (Application.platform == RuntimePlatform.Android)
        {
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);

                if (touch.phase == TouchPhase.Began)
                {
                    GameManager.SCENE.LoadScene(Define.Scenes.IN_GAME);
                }
            }
        }
        else
        {
            if (Input.GetKeyDown(KeyCode.Return))
            {
                GameManager.SCENE.LoadScene(Define.Scenes.IN_GAME);
            }
        }
    }

}