using Character;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UI_HUD : UI_Scene
{
    public BaseCharacter targetChar;

    [SerializeField]
    private Image HpBar;

    public override void Init()
    {
        //base.Init();
    }

    protected override void Start()
    {
        HpBar = this.gameObject.FindChild<Image>("Hp", true);
    }

    private void Update()
    {
        DecreaseHpBar();        
    }

    private void DecreaseHpBar()
    {
        if (targetChar)
        {
            HpBar.fillAmount = Mathf.Lerp(HpBar.fillAmount, targetChar.HP / 100f, Time.deltaTime * 2f);
        }
    }

}
