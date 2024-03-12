using Google.Protobuf.Protocol;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BakalScene : BaseScene
{
    protected override void Initialize()
    {
        GameManager.SCENE.CurrentScene = Define.Scenes.BAKAL;

        GameManager.Sound.BGMStop();
        GameManager.Sound.Play("Sounds/Bakal", Define.SoundType.BGM);
        
    }    
}
