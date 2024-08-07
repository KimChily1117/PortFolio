using Character;
using Google.Protobuf.Protocol;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public enum TownMapState
{
    SERIAROOM,
    DUNGEONENTRANCE
}






public class TownScene : BaseScene
{
    public TownMapState CurrentMapState { get; set; }


    public TriggerEvent _seriaTriggerEvent;
    public TriggerEvent _dungeonTriggerEvent;

    public Transform _dungeonEntranceSpawn;
    private UI_PartyEntry PartyEntry { get; set; }

    public CameraController _cameraController;
    protected override void Initialize()
    {
        Debug.Log($"IngameScene ] InGameScene!!! ");

        GameManager.SCENE.CurrentScene = Define.Scenes.TOWN;

        CurrentMapState = TownMapState.SERIAROOM;

        GameManager.Network.ConnectToGame();

        //배경음 및 온갖것들 다 초기화
        GameManager.Sound.BGMStop();
        GameManager.Sound.Play("Sounds/seria_gate", Define.SoundType.BGM);
        GameManager.Sound.SetAudioVolume(0.55f);

        if(GameManager.PlayerPlatformType == Enums.PlatformType.MOBILE)
        {
            GameManager.UI.ShowSceneUI<UI_MobileController>("Dynamic_Joystick");
        }

        if (_seriaTriggerEvent == null)
        {
            _seriaTriggerEvent = GameObject.Find("SeriaPortal").GetComponent<TriggerEvent>();
        }

        if (_dungeonTriggerEvent == null)
        {
            _dungeonTriggerEvent = GameObject.Find("BakalPortal").GetComponent<TriggerEvent>();
        }

        _seriaTriggerEvent.ClearTriggerEvent();
        _dungeonTriggerEvent.ClearTriggerEvent();
        _seriaTriggerEvent.AddTriggerEnterEvent(SeriaTriggerEnterEvent);
        _dungeonTriggerEvent.AddTriggerEnterEvent(DungeonTriggerEnterEvent);


        _cameraController.SetCameraLimit(CurrentMapState);
        GameManager.UI.ShowSceneUI<UI_HUD>("HUD");

    }


    private void SeriaTriggerEnterEvent(Collider2D collider)
    {
        Debug.Log("Seria Trigger Enter!!");
        
        if (collider.CompareTag("Enemy"))
            return;

        if (collider.CompareTag("Player"))
        {
            CurrentMapState = TownMapState.DUNGEONENTRANCE;
            _cameraController.SetCameraLimit(CurrentMapState);
            GameManager.Sound.BGMStop();
            GameManager.Sound.Play("Sounds/bakal_ready", Define.SoundType.BGM);
        }


        collider.transform.parent.position = _dungeonEntranceSpawn.position;
    }

    private void DungeonTriggerEnterEvent(Collider2D collider)
    {
        Debug.Log("Dungeon Trigger Enter!!");

        if (collider.CompareTag("OtherPlayer"))
            return;

        if (collider.CompareTag("Player"))
        {

            C_CreateRoom c_room = new C_CreateRoom();
            c_room.Playerinfo = collider.transform.parent.GetComponent<MyPlayer>().ObjInfo;
            GameManager.Network.Send(c_room);

            //Destroy(collider.gameObject);
        }

        _dungeonTriggerEvent.ClearTriggerEvent();
     }


}
