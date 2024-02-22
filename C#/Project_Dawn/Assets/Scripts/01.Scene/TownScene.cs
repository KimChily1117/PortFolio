using Character;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TownScene : BaseScene
{
    public TriggerEvent triggerEvent;


    protected override void Initialize()
    {
        GameManager.SCENE.CurrentScene = Define.Scenes.TOWN;


        Debug.Log($"IngameScene ] InGameScene!!! ");

        //배경음 및 온갖것들 다 초기화
        GameManager.Sound.BGMStop();
        GameManager.Sound.Play("Sounds/seria_gate", Define.SoundType.BGM);


        triggerEvent.AddTriggerEnterEvent(TriggerEnterEvent);

    }


    private void TriggerEnterEvent(Collider2D collider)
    {
        Debug.Log("Trigger Enter!!");

        GameManager.SCENE.LoadScene(Define.Scenes.BAKAL);
    }
}
