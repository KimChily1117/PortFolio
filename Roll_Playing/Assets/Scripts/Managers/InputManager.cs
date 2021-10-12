using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InputManager 
{
    public Action _keyAction = null;
    public string singleton_test = "";
    
    public void OnUpdate()
    {       

        // 키입력이 없을 때(어짜피 Update문에서 계속 검출하고있음.
        if (Input.anyKey == false) { return; }
        if (_keyAction != null)
        {
            _keyAction.Invoke();
        }
    }

}
