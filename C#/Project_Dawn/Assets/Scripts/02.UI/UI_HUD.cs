using Character;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UI_HUD : UI_Scene 
{
    public BaseCharacter targetChar;

    [SerializeField]
    private Image HpBar;

    public Button _invenBtn;
    public Button _StatBtn;


    public override void Init()
    {
        base.Init();
    }

    protected override void Start()
    {
        base.Start();
        Init();
        HpBar = this.gameObject.FindChild<Image>("Hp", true);
        _invenBtn = this.gameObject.FindChild<Button>("BtnInventory", true);
        _StatBtn = this.gameObject.FindChild<Button>("BtnStat", true);
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
