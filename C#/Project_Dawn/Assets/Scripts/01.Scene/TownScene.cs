using Character;
using Google.Protobuf.Protocol;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TownScene : BaseScene
{
    public TriggerEvent _triggerEvent;

    private UI_PartyEntry PartyEntry { get; set; }

    protected override void Initialize()
    {
        GameManager.SCENE.CurrentScene = Define.Scenes.TOWN;


        Debug.Log($"IngameScene ] InGameScene!!! ");

        //배경음 및 온갖것들 다 초기화
        GameManager.Sound.BGMStop();
        GameManager.Sound.Play("Sounds/seria_gate", Define.SoundType.BGM);

        GameManager.UI.ShowSceneUI<UI_HUD>("HUD");


        _triggerEvent.AddTriggerEnterEvent(TriggerEnterEvent);
    }


    private void TriggerEnterEvent(Collider2D collider)
    {
        Debug.Log("Trigger Enter!!");

        /*
         Todo : 
         */


        C_CreateRoom c_room = new C_CreateRoom();
        c_room.Playerinfo = GameManager.ObjectManager.MyPlayer.ObjInfo;
        GameManager.Network.Send(c_room);


        //PartyEntry = GameManager.UI.ShowPopupUI<UI_PartyEntry>("PartyPopUp");

        //GameManager.SCENE.LoadScene(Define.Scenes.BAKAL);
    }
}
