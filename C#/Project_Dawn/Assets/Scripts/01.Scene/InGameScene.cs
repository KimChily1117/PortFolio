using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InGameScene : BaseScene
{
    //(todo)여기서 Network와 통신해서 데이터를 파싱하고 장비 , 스텟 등 플레이어 데이터를 받아온다.

    protected override void Initialize()
    {

        _currentScene = Define.Scenes.DUNGEON;
        Debug.Log($"IngameScene ] InGameScene!!! ");
        
        // 배경음 및 온갖것들 다 초기화
        GameManager.Sound.BGMStop();
        GameManager.Sound.Play("Sounds/AshyGraveyard",Define.SoundType.BGM);


    }
}
