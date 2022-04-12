using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UI_Buttons : UI_Base
{    
    void Start()
    {
        Bind<Button>(typeof(Buttons)); // typeof�� ����ȯ�� �Ͽ� C#���� �⺻���� �����ϴ� object Ŭ������ ��ӹ޴� ��ü�� �Ű������� �ѱ��.
        Bind<Text>(typeof(Texts)); // typeof�� ����ȯ�� �Ͽ� C#���� �⺻���� �����ϴ� object Ŭ������ ��ӹ޴� ��ü�� �Ű������� �ѱ��.

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
