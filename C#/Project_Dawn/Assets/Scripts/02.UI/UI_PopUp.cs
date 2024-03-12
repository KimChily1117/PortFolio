 using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UI_PopUp : UI_Base
{   
    protected RectTransform rectTransform { get; set; }


    public override void Init()
    {
        GameManager.UI.SetCanvas(false);
        rectTransform = GetComponent<RectTransform>();
    }

    public virtual void ClosePopupUI()
    {
        GameManager.UI.ClosePopupUI(this);      
    }    
} 
