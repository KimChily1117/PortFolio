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

        // Ű�Է��� ���� ��(��¥�� Update������ ��� �����ϰ�����.
        if (Input.anyKey == false) { return; }
        if (_keyAction != null)
        {
            _keyAction.Invoke();
        }
    }

}
