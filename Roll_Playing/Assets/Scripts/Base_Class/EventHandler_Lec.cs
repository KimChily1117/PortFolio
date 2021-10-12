using KSY_UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;


public class EventHandler_Lec : UI_Base
{

    Action<string> MethodActionTest; // <T>에 있는 인자값을 구독한 함수에다가 전달해드립니다. 라는뜻
   
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

        Initialize("A파라미터",
            "B파라미터",
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
        return value.Equals(5); // value(인자값)이 5와같다면 true를 반환해라.
    }

    public void Action_test(string value)
    {
        Debug.Log("Action Test " + value);
    }

    public void Initialize(string a,string b,bool c ,Action<string> callbackMessage)
    {
        Debug.Log($"잘 되었는가?  {a},{b},{c}");

        StringBuilder SB = new StringBuilder(); // 스트링빌더가 조금 더 빠르다. by jin

        SB.Append(a);
        SB.Append(b);
        SB.Append(c);

        callbackMessage.Invoke(SB.ToString());        
    }
    
}
