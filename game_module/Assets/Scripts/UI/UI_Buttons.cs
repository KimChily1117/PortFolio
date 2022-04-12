using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UI_Buttons : UI_Base
{    
    void Start()
    {
        Bind<Button>(typeof(Buttons)); // typeof로 형변환을 하여 C#에서 기본으로 제공하는 object 클래스를 상속받는 객체를 매개변수로 넘긴다.
        Bind<Text>(typeof(Texts)); // typeof로 형변환을 하여 C#에서 기본으로 제공하는 object 클래스를 상속받는 객체를 매개변수로 넘긴다.

        Get<Text>((int)Texts.PointText).text = "Getter Test";
        Get<Button>((int)Buttons.PointButton).onClick.AddListener(() => { Get<Text>((int)Texts.PointText).text = "BTN CLICK"; });

        GameManager.Input.KeyAction -= InputTest;
        GameManager.Input.KeyAction += InputTest;
    }

    public void InputTest()
    {
        if(Input.GetKey(KeyCode.Tab))
        {
            GetText((int)Texts.PointText).text = "OnClickBtn Tab";
        }

    }

}
