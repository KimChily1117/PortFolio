using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BakalScene : BaseScene
{
    protected override void Initialize()
    {
        //GameManager.Input.Clear();
        GameManager.SCENE.CurrentScene = Define.Scenes.BAKAL;

        GameManager.Sound.BGMStop();
        GameManager.Sound.Play("Sounds/Bakal", Define.SoundType.BGM);
    }    
}
