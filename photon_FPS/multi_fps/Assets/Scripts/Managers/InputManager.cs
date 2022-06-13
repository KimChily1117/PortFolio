using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InputManager
{
    public Action KeyAction = null;
    public Action<Define.MouseEvent> MouseAction = null; // Define에 선언되어 있는 Enum으로 마우스 KeyEvent만 적출해낸다. 

    bool _pressed = false;
    public void OnUpdate()
    {
        if (Input.anyKey && KeyAction != null)
        {
            KeyAction.Invoke(); // 등록된 함수를 실행시킨다.
        }

        if (MouseAction != null)
        {
            if (Input.GetMouseButton(0)|| Input.GetTouch(0).phase == TouchPhase.Began)
            {
                Debug.Log($"OnClick Left Mouse Button case : press");
                MouseAction.Invoke(Define.MouseEvent.Press);
                _pressed = true;
            }

            else
            {
                if (_pressed)
                {
                    Debug.Log($"OnClick Left Mouse Button case : click");
                    MouseAction.Invoke(Define.MouseEvent.Click);
                    _pressed = false;
                }
            }
        }

    }







}
