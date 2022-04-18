using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class UI_Buttons : UI_PopUp
{    
    void Start()
    {
        Bind<Button>(typeof(Buttons)); // typeof로 형변환을 하여 C#에서 기본으로 제공하는 object 클래스를 상속받는 객체를 매개변수로 넘긴다.
        Bind<Text>(typeof(Texts)); // typeof로 형변환을 하여 C#에서 기본으로 제공하는 object 클래스를 상속받는 객체를 매개변수로 넘긴다.
        Bind<Image>(typeof(Images));


        Get<Button>((int)Buttons.PointButton).gameObject.BindEvent(OnClickButtonEvent);

        GameObject go = GetImage((int)Images.IconTest).gameObject;

        go.BindEvent((PointerEventData data) => { go.transform.position = data.position; }, Define.UIEvent.Drag);

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

    int _count = 0;
    public void OnClickButtonEvent(PointerEventData data)
    {
        _count++;

        Get<Text>((int)Texts.PointText).text = $"버튼을 누르면 올라갑니다->{_count}";

    }

}
