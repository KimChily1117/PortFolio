using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class InputManager
{
    public Action KeyDownAction = null; 
    // Key瑜??뚮??????숈옉??Invoke ?쒗궎湲??꾪빐 ?좎뼵
    
    public Action KeyUpAction = null;
    // Key瑜??ъ쓣 ???숈옉??Invoke ?쒗궎湲??꾪빐 ?좎뼵 
    
    public Action<Define.MouseEvent> MouseAction = null;

    public Action<float, float> TouchAction = null;
    public Action TouchAttackAction = null;
    public Action TouchJumpAction = null;

    public Action<PointerEventData> EndDragAction = null;


    // Double Input 
    public float lastInputElapsed = 0f;
    
    //??踰??뚮윭???좎????곹깭
    public bool DoublePressed { get; private set; }

    private bool doubleInputPressed;
    
    public float doubleInputThreshold = 0.2f;

    private KeyCode _inputKeycode;

    public Action DoubleKeyAction = null;
    // Key瑜??ъ쓣 ???숈옉??Invoke ?쒗궎湲??꾪빐 ?좎뼵 

    
    public Action<Define.InputType> inputTypeAction = null;

    public bool IsInputLocked { get; private set; }


    private float _horizontal;
    private float _vertical;


    bool _pressed = false;
    public bool _keypressed = false;

    public void SetInputLocked(bool locked)
    {
        if (IsInputLocked == locked)
            return;

        IsInputLocked = locked;
        doubleInputPressed = false;
        DoublePressed = false;
        _keypressed = false;
        _pressed = false;
        Debug.Log($"[INPUT][LOCK] Locked={IsInputLocked}");
    }
    public void OnUpdate()
    {
        if (IsInputLocked)
            return;

        // if(EventSystem.current.IsPointerOverGameObject() == true)
        // {   
        //     //Debug.Log($"OnClick Over UI Object");
        //     return;
        // }
        // 3D 寃뚯엫???꾨땲湲??뚮Ц??臾몄젣?놁쓣寃껋쑝濡??앷컖??
        
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
                        KeyUpAction?.Invoke();
                        doubleInputPressed = false;
                        DoublePressed = false;
                    }

                    return;
                }

                // double input??議곌굔 -> ?쒖떆???쒓컙(0.3珥??대궡濡??ㅼ떆 ?낅젰???섎㈃ ??ъ? 媛숈? ?낅젰??泥섎━ ???섏엲?? 
                doubleInputPressed = Time.time - lastInputElapsed  < doubleInputThreshold;
                lastInputElapsed = Time.time; 
            }
            
            if (Input.GetKey(_inputKeycode))
            {
                if (doubleInputPressed)
                {
                    DoublePressed = true;
                    DoubleKeyAction?.Invoke();
                }                
            }

            else
            {
                if (_keypressed)
                {
                    Debug.Log(($"Pressed"));
                    _keypressed = false;
                    KeyUpAction?.Invoke();
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
        ClearPlayerInputCallbacks();
    }

    public void ClearPlayerInputCallbacks()
    {
        KeyDownAction = null;
        KeyUpAction = null;
        DoubleKeyAction = null;
        TouchAction = null;
        TouchAttackAction = null;
        TouchJumpAction = null;
        EndDragAction = null;
        inputTypeAction = null;
        doubleInputPressed = false;
        DoublePressed = false;
        _keypressed = false;
        Debug.Log("[INPUT][CLEAR_PLAYER_INPUT_CALLBACKS]");
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

