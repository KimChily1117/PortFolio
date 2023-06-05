using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InputManager
{
    public Action KeyAction = null;
    public Action<Define.MouseEvent> MouseAction = null; // Define�� ����Ǿ� �ִ� Enum���� ���콺 KeyEvent�� �����س���. 

    bool _pressed = false;
    public void OnUpdate()
    {
        if (Input.anyKey && KeyAction != null)
        {
            KeyAction.Invoke(); // ��ϵ� �Լ��� �����Ų��.
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
