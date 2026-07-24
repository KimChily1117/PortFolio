using Character;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UI_BakalSceneUI : UI_Scene
{
    public UI_HUD HUD { get; private set; }

    // Bakal Boss
    public BaseCharacter targetChar;

    [SerializeField]
    private Image HpBar;

    [SerializeField]
    private Image BackHpBar;

    public RectTransform rectTransform;


    public bool isDecrease;

    protected override void OnEnable()
    {
        base.OnEnable();
        Init();
        
        rectTransform = GetComponent<RectTransform>();

    }


    public override void Init()
    {
        base.Init();

        HpBar = this.gameObject.FindChild<Image>("Hp", true);
        BackHpBar = this.gameObject.FindChild<Image>("BackHpBar", true);
        DisableEmbeddedHud();
    }


    public void SetExternalHud(UI_HUD hud)
    {
        HUD = hud;
    }

    private void DisableEmbeddedHud()
    {
        UI_HUD embeddedHud = this.gameObject.FindChild<UI_HUD>("HUD", true);
        if (embeddedHud == null)
            return;

        embeddedHud.gameObject.SetActive(false);
    }
    protected override void Start()
    {
        base.Start();
        rectTransform.anchoredPosition = Vector2.zero;

    }

    private void Update()
    {
        DecreaseHpBar();
        AfterDecreaseHpBar();
    }
    
    private void DecreaseHpBar()
    {
        if (targetChar)
        {
            float maxHp = Mathf.Max(1f, targetChar.MaxHP);
            float hpRatio = Mathf.Clamp01(targetChar.HP / maxHp);
            HpBar.fillAmount = Mathf.Lerp(HpBar.fillAmount, hpRatio, Time.deltaTime * 3f);
        }
    }


    private void AfterDecreaseHpBar()
    {
        if (isDecrease)
        {
            BackHpBar.fillAmount = Mathf.Lerp(BackHpBar.fillAmount, HpBar.fillAmount, Time.deltaTime * 3f);

            if (HpBar.fillAmount >= BackHpBar.fillAmount - 0.01f)
            {
                isDecrease = false;
                BackHpBar.fillAmount = HpBar.fillAmount;
            }
        }
    }

}


