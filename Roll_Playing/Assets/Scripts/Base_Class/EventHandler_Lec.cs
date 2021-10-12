using KSY_UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;


public class EventHandler_Lec : UI_Base
{

    Action<string> MethodActionTest; // <T>�� �ִ� ���ڰ��� ������ �Լ����ٰ� �����ص帳�ϴ�. ��¶�
   
    private void Start()
    {

        m_testInteger = 0;
        test_btn.onClick.AddListener(() => OnClickbtn());
        text.text = "Empty Text";

        MethodActionTest += Action_test;
    }


    public void OnClickbtn()
    {
        Debug.Log("AddListener Test");
        m_testInteger = 5;
        text.text = m_testInteger.ToString();

        Initialize("A�Ķ����",
            "B�Ķ����",
            BooleanTest(m_testInteger),
            jsonMassage =>
        {
            GetLambda(jsonMassage);
            MethodActionTest(jsonMassage);            
            
        });
    }
        

    public void GetLambda(string _callbackMassage)
    {
        Debug.Log($"callback Practice : {_callbackMassage}");
        
    }


    public bool BooleanTest(int value)
    {
        return value.Equals(5); // value(���ڰ�)�� 5�Ͱ��ٸ� true�� ��ȯ�ض�.
    }

    public void Action_test(string value)
    {
        Debug.Log("Action Test " + value);
    }

    public void Initialize(string a,string b,bool c ,Action<string> callbackMessage)
    {
        Debug.Log($"�� �Ǿ��°�?  {a},{b},{c}");

        StringBuilder SB = new StringBuilder(); // ��Ʈ�������� ���� �� ������. by jin

        SB.Append(a);
        SB.Append(b);
        SB.Append(c);

        callbackMessage.Invoke(SB.ToString());        
    }
    
}
