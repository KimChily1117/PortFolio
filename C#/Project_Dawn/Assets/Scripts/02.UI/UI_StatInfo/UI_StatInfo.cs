using Data;
using Google.Protobuf.Protocol;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UI_StatInfo : UI_PopUp
{
    enum Images
    {
        Slot_Armor,
        Slot_Weapon,
        Slot_Boots
    }

    enum Texts
    {
        NameText,
        AttackValueText,
        DefenceValueText
    }

    bool _isInit = false;

    public override void Init()
    {
        base.Init();
        Bind<Image>(typeof(Images));
        Bind<Text>(typeof(Texts));

        _isInit = true;
        RefreshUI();
    }
    private void Awake()
    {
        Init();
    }


    public void RefreshUI()
    {
        if (_isInit == false)
            return;

        // 우선은 다 가린다
        Get<Image>((int)Images.Slot_Armor).enabled = false;
        Get<Image>((int)Images.Slot_Weapon).enabled = false;
        Get<Image>((int)Images.Slot_Boots).enabled = false;

        // Image 채워준다
        foreach (Item item in GameManager.Inven.Items.Values)
        {
            if (item.Equipped == false)
                continue;

            ItemData itemData = null;
            GameManager.DataManager.ItemDict.TryGetValue(item.TemplateId, out itemData);

            Sprite icon = GameManager.Resources.Load<Sprite>(itemData.iconPath);

            if (item.ItemType == ItemType.Weapon)
            {
                Get<Image>((int)Images.Slot_Weapon).enabled = true;
                Get<Image>((int)Images.Slot_Weapon).sprite = icon;
            }

            else if (item.ItemType == ItemType.Armor)
            {
                Armor armor = (Armor)item;
                switch (armor.ArmorType)
                {
                    case ArmorType.Armor:
                        Get<Image>((int)Images.Slot_Armor).enabled = true;
                        Get<Image>((int)Images.Slot_Armor).sprite = icon;
                        break;
                    case ArmorType.Boots:
                        Get<Image>((int)Images.Slot_Boots).enabled = true;
                        Get<Image>((int)Images.Slot_Boots).sprite = icon;
                        break;
                }
            }

            // Text
            MyPlayer player = (MyPlayer)GameManager.ObjectManager.MyPlayer;

            player.RefreshAdditionalStat();

            //Get<Text>((int)Texts.NameText).text = player.name;

            int totalDamage = 10 + player.WeaponDamage;
            Get<Text>((int)Texts.AttackValueText).text = $"{10}(+{player.WeaponDamage})";
            Get<Text>((int)Texts.DefenceValueText).text = $"{player.ArmorDefence}";
            
        
        }


    }
}
