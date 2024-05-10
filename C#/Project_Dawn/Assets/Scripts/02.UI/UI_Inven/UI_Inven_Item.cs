using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UI_Inven_Item : UI_Base
{
    [SerializeField]
    Image _icon;

    public override void Init()
    {

    }

    public void SetItem(int templateId, int count)
    {
        Data.ItemData itemData = null;
        GameManager.DataManager.ItemDict.TryGetValue(templateId, out itemData);

        Sprite icon = GameManager.Resources.Load<Sprite>(itemData.iconPath);
        _icon.sprite = icon;
    }
}
