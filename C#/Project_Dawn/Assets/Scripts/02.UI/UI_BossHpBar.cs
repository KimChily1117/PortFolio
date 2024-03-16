using Character;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UI_BossHpBar : UI_Scene
{
    public BaseCharacter targetChar;

    [SerializeField]
    private Image HpBar;

    [SerializeField]
    private Image BackHpBar;


    public bool isDecrease;

    protected override void OnEnable()
    {
        base.OnEnable();
    }


    public override void Init()
    {
        base.Init();

        HpBar = this.gameObject.FindChild<Image>("Hp", true);
        BackHpBar = this.gameObject.FindChild<Image>("BackHpBar", true);
    }

    protected override void Start()
    {
        base.Start();
        Init();
    }

    private void Update()
    {
        DecreaseHpBar();

        // Todo : ü�¹� �帣�� ȿ�� , �����ϴ� ȿ�� ���� �� �̻ڰ� ����
        
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

