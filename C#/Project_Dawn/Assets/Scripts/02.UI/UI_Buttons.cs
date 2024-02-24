using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class UI_Buttons : UI_Base
{

    public override void Init()
    {
        
    }
    protected override void Start()
    {

        Bind<Button>(typeof(Buttons)); // typeof로 형변환을 하여 C#에서 기본으로 제공하는 object 클래스를 상속받는 객체를 매개변수로 넘긴다.
        Bind<Text>(typeof(Texts)); // typeof로 형변환을 하여 C#에서 기본으로 제공하는 object 클래스를 상속받는 객체를 매개변수로 넘긴다.
        Bind<Image>(typeof(Images));

        //Get<Button>((int)Buttons.PointButton).gameObject.BindEvent(OnClickButtonEvent);
        // get함수를 사용해서 게임 오브젝트를 받아온다음에. 그 GameObject에다가 event를 bind해준다.
        GameObject go = GetImage((int)Images.IconTest).gameObject;
        // go에다가 bind 시킨후 casting이 되었는지 확인 필요

        Debug.Log($"UI_Buttons ] 게임 오브젝트 바인딩 잘되었는가요? : {go.name}");

        go.BindEvent((PointerEventData data) => { go.transform.position = data.position; }, Define.UIEvent.Drag);

        GameManager.Input.KeyDownAction -= InputTest; // when input keycode.tab change UI Element
        GameManager.Input.KeyDownAction += InputTest;
    }

    public void InputTest()
    {
        if(Input.GetKey(KeyCode.Tab))
        {
            //GetText((int)Texts.PointText).text = "OnClickBtn Tab";
        }
    }

    int _count = 0;
    public void OnClickButtonEvent(PointerEventData data)
    {
        _count++;
        //Get<TextMeshProUGUI>((int)Texts.PointText).text = $"버튼을 누르면 올라갑니다->{_count}";
     
        
    }

   

}
