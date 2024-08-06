using Google.Protobuf.Protocol;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BakalScene : BaseScene
{
    protected override void Initialize()
    {


        GameManager.SCENE.CurrentScene = Define.Scenes.BAKAL;

        GameManager.ObjectManager.Clear();
        
        GameManager.Sound.BGMStop();


        if (GameManager.PlayerPlatformType == Enums.PlatformType.MOBILE)
        {
            GameManager.UI.ShowSceneUI<UI_MobileController>("Dynamic_Joystick");
        }



        GameManager.Sound.Play("Sounds/Bakal", Define.SoundType.BGM);
        GameManager.Sound.SetAudioVolume(0.55f);


        GameManager.Sound.Play("Sounds/mon/bakal/bakal_dragon_meet");
        // 바칼 조우 effect 소리 들어가야함
        GameManager.UI.ShowSceneUI<UI_BakalSceneUI>("BossHpBar");

       
    }
}
