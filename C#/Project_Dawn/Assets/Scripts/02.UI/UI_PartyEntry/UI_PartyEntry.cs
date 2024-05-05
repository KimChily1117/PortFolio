using Google.Protobuf.Collections;
using Google.Protobuf.Protocol;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UI_PartyEntry : UI_PopUp
{
    List<LobbyPlayerInfo> _alreadyPlayerInfos = new List<LobbyPlayerInfo>();

    bool _isMaster = false;

    public override void Init()
    {
        base.Init();
        Bind<Button>(typeof(Buttons));
        Bind<TextMeshProUGUI>(typeof(Texts));


        GetButton((int)Buttons.StartButton).gameObject.BindEvent(OnClickEnterBtn, Define.UIEvent.Click);
    }
    public void SetUIElement(bool isMaster, string MemberTxt = "")
    {
        rectTransform.anchoredPosition = new Vector2(0, 0);


        if (isMaster)
        {
            Get<TextMeshProUGUI>((int)Texts.StartBtnText).text = "Start";
            Get<TextMeshProUGUI>((int)Texts.StartBtnText).color = Color.red;
            Get<TextMeshProUGUI>((int)Texts.Slot_1_PlayerName).text = MemberTxt;
        }
        else
        {
            Get<TextMeshProUGUI>((int)Texts.StartBtnText).text = "Ready";
            Get<TextMeshProUGUI>((int)Texts.StartBtnText).color = Color.yellow;
            // 나머지 OtherPlayer들의 Text도 넣어줘야함

        }


    }

    public void SetUIElement(RepeatedField<LobbyPlayerInfo> playerInfos = null)
    {
        _alreadyPlayerInfos.Clear();

        foreach (LobbyPlayerInfo item in playerInfos)
        {
            _alreadyPlayerInfos.Add(item);
        }



        rectTransform.anchoredPosition = new Vector2(0, 0);
        string[] SlotArr = Enum.GetNames(typeof(Texts));

        int SlotCounts = SlotArr.Length;

        _isMaster = GameManager.MyName == _alreadyPlayerInfos[0].Name;

        if (_isMaster)
        {
            Get<TextMeshProUGUI>((int)Texts.StartBtnText).text = "Start";
            Get<TextMeshProUGUI>((int)Texts.StartBtnText).color = Color.red;

        }
        else
        {
            Get<TextMeshProUGUI>((int)Texts.StartBtnText).text = "Ready";
            Get<TextMeshProUGUI>((int)Texts.StartBtnText).color = Color.yellow;
        }


        for (int i = 1; i <= _alreadyPlayerInfos.Count; i++)
        {
            if (!String.IsNullOrEmpty(_alreadyPlayerInfos[i - 1].Name))
                Get<TextMeshProUGUI>(i).text = _alreadyPlayerInfos[i - 1].Name;
        }

    }

    protected override void OnEnable()
    {
        base.OnEnable();
        Init();
    }

    protected override void Start()
    {
        base.Start();
    }

    #region Button Interaction
    public void OnClickEnterBtn(PointerEventData evt)
    {

        C_SceneMove c_Scene_Move = new C_SceneMove();

        if (c_Scene_Move.Playerinfo == null)
        {
            c_Scene_Move.Playerinfo = new ObjectInfo();
        }
        
        c_Scene_Move.Playerinfo.Name = GameManager.MyName;
        c_Scene_Move.Playerinfo.IsMaster = true;



        GameManager.Network.Send(c_Scene_Move);

    }

    #endregion Button Interaction


}
