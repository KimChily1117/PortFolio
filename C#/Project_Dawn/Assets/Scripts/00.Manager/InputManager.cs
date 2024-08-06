using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class InputManager
{
    public Action KeyDownAction = null; 
    // Key를 눌렀을 떄 동작을 Invoke 시키기 위해 선언
    
    public Action KeyUpAction = null;
    // Key를 뗬을 때 동작을 Invoke 시키기 위해 선언 
    
    public Action<Define.MouseEvent> MouseAction = null;

    public Action<float, float> TouchAction = null;
    public Action TouchAttackAction = null;
    public Action TouchJumpAction = null;

    public Action<PointerEventData> EndDragAction = null;


    // Double Input 
    public float lastInputElapsed = 0f;
    
    //두 번 눌러서 유지한 상태
    public bool DoublePressed { get; private set; }

    private bool doubleInputPressed;
    
    public float doubleInputThreshold = 0.2f;

    private KeyCode _inputKeycode;

    public Action DoubleKeyAction = null;
    // Key를 뗬을 때 동작을 Invoke 시키기 위해 선언 

    
    public Action<Define.InputType> inputTypeAction = null;


    private float _horizontal;
    private float _vertical;


    bool _pressed = false;
    public bool _keypressed = false;
    public void OnUpdate()
    {
        // if(EventSystem.current.IsPointerOverGameObject() == true)
        // {   
        //     //Debug.Log($"OnClick Over UI Object");
        //     return;
        // }
        // 3D 게임이 아니기 때문에 문제없을것으로 생각됨
        
        if (KeyDownAction != null)
        {
            if (Input.GetMouseButton(0)) { return; }


            if (Input.anyKey)
            {
                
                KeyDownAction.Invoke();
                _keypressed = true;
            }
            
            if (Input.GetKeyDown(_inputKeycode))
            {                
                if(CheckAtkButton(_inputKeycode))
                {

                    //inputTypeAction.Invoke(Define.InputType.ATK);

                    if (_keypressed)
                    {
                        Debug.Log(($"Pressed"));
                        _keypressed = false;
                        KeyUpAction.Invoke();
                        doubleInputPressed = false;
                        DoublePressed = false;
                    }

                    return;
                }

                // double input의 조건 -> 제시된 시간(0.3초)이내로 다시 입력을 하면 대쉬와 같은 입력을 처리 할 수잇다. 
                doubleInputPressed = Time.time - lastInputElapsed  < doubleInputThreshold;
                lastInputElapsed = Time.time; 
            }
            
            if (Input.GetKey(_inputKeycode))
            {
                if (doubleInputPressed)
                {
                    DoublePressed = true;
                    DoubleKeyAction.Invoke();
                }                
            }

            else
            {
                if (_keypressed)
                {
                    Debug.Log(($"Pressed"));
                    _keypressed = false;
                    KeyUpAction.Invoke();
                    doubleInputPressed = false;
                    DoublePressed = false;
                }
            }
        }

        if (MouseAction != null)
        {
            if(Input.GetMouseButton(0))
            {
                MouseAction.Invoke(Define.MouseEvent.Press);
                _pressed = true;
            }

            else
            {
                if (_pressed)
                {
                    MouseAction.Invoke(Define.MouseEvent.Click);
                    _pressed = false;
                }
            }
        }

        if(TouchAction != null)
        {
            TouchAction.Invoke(_horizontal, _vertical);
        }
    }
    public void Clear()
    {
        MouseAction = null;
        KeyDownAction = null;
        KeyUpAction = null;

        TouchAction = null;
    }



    public void JoyStickActionReciver(float h, float v)
    {
        _horizontal = h;
        _vertical = v;
    }


    public void SetInputKeyCode(KeyCode code)
    {
        _inputKeycode = code;
    }
    private bool CheckAtkButton(KeyCode code)
    {
        return code == KeyCode.X;
    }
    
}
