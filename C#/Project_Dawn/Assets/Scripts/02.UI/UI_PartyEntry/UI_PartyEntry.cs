using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UI_PartyEntry : UI_PopUp
{

   

    public override void Init()
    {
        base.Init();

        rectTransform.anchoredPosition = new Vector2(0,0);

        Bind<Button>(typeof(Buttons));
        Bind<TextMeshProUGUI>(typeof(Texts));


        Get<TextMeshProUGUI>((int)Texts.StartBtnText).text = "I'm 미미밍터";


        GetButton((int)Buttons.StartButton).gameObject.BindEvent(OnClickEnterBtn, Define.UIEvent.Click);
    }

    protected override void Start()
    {
        base.Start();
        Init();
    }



    #region Button Interaction

    public void OnClickEnterBtn(PointerEventData evt)
    {
        Debug.Log($"OnClick!!! Start or Ready btn!!!");
    }


    #endregion Button Interaction


}
