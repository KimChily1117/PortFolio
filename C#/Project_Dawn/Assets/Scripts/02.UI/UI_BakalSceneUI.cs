using Character;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UI_BakalSceneUI : UI_Scene
{
    public UI_HUD HUD { get; set; }

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
        HUD = this.gameObject.FindChild<UI_HUD>("HUD", true);
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
            HpBar.fillAmount = Mathf.Lerp(HpBar.fillAmount, targetChar.HP / 100f, Time.deltaTime * 3f);
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

