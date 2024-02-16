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


        //triggerEvent.AddTriggerEnterEvent.

    }


    private void TriggerEnterEvent(Collider collider)
    {
        Debug.Log(collider.GetComponent<BaseCharacter>().Id);

        GameManager.SCENE.LoadScene(Define.Scenes.BAKAL);
    }
}
