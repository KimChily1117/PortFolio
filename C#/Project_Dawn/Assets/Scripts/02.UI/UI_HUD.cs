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
        ConfigureHpBarImage();
        _invenBtn = this.gameObject.FindChild<Button>("BtnInventory", true);
        _StatBtn = this.gameObject.FindChild<Button>("BtnStat", true);
    }
    private void ConfigureHpBarImage()
    {
        if (HpBar == null)
            return;

        HpBar.type = Image.Type.Filled;
        HpBar.fillMethod = Image.FillMethod.Vertical;
        HpBar.fillOrigin = (int)Image.OriginVertical.Bottom;
        HpBar.fillClockwise = true;
    }

    private void Update()
    {
        BindTargetIfMissing();
        DecreaseHpBar();        
    }
    public void RefreshHpBarImmediate()
    {
        BindTargetIfMissing();
        if (targetChar == null || HpBar == null)
            return;

        ConfigureHpBarImage();
        float maxHp = Mathf.Max(1f, targetChar.MaxHP);
        HpBar.fillAmount = Mathf.Clamp01(targetChar.HP / maxHp);
    }

    private void BindTargetIfMissing()
    {
        if (targetChar != null || GameManager.ObjectManager == null)
            return;

        targetChar = GameManager.ObjectManager.MyPlayer;
    }

    private void DecreaseHpBar()
    {
        if (targetChar)
        {
            float maxHp = Mathf.Max(1f, targetChar.MaxHP);
            HpBar.fillAmount = Mathf.Lerp(HpBar.fillAmount, Mathf.Clamp01(targetChar.HP / maxHp), Time.deltaTime * 2f);
        }
    }

}







