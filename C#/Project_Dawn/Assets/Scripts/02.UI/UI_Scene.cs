using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UI_Scene : UI_Base
{
    //public UI_Stat StatUI { get; private set; }


    private void Awake()
    {
        Init();
    }

    public override void Init()
    {        
        GameManager.UI.SetCanvas(false);

        //StatUI = GetComponentInChildren<UI_Stat>();
        //StatUI.gameObject.SetActive(false);
    }  

}
