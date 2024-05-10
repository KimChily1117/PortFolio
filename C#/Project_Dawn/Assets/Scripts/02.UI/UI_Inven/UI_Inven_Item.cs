using Google.Protobuf.Protocol;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UI_Inven_Item : UI_Base
{
    [SerializeField]
    Image _icon;
    [SerializeField]
    Image _frame = null;


    public int ItemDbId { get; private set; }
    public int TemplateId { get; private set; }
    public int Count { get; private set; }
    public bool Equipped { get; private set; }

    private void Start()
    {
        Init();
    }
    public override void Init()
    {
        _icon.gameObject.BindEvent(e =>
        {
            Debug.Log("Click Item");

            Data.ItemData itemData = null;
            GameManager.DataManager.ItemDict.TryGetValue(TemplateId, out itemData);

            // TODO : C_USE_ITEM 아이템 사용 패킷
            if (itemData.itemType == ItemType.Consumable)
                return;

            C_EquipItem equipPacket = new C_EquipItem();
            equipPacket.ItemDbId = ItemDbId;
            equipPacket.Equipped = !Equipped;

            GameManager.Network.Send(equipPacket);
        });
    }

    public void SetItem(Item item)
    {

        ItemDbId = item.ItemDbId;
        TemplateId = item.TemplateId;
        Count = item.Count;
        Equipped = item.Equipped;


        Data.ItemData itemData = null;
        GameManager.DataManager.ItemDict.TryGetValue(TemplateId, out itemData);

        Sprite icon = GameManager.Resources.Load<Sprite>($"{itemData.iconPath}");
        _icon.sprite = icon;

        // Todo ItemInfo 기입

        _frame.gameObject.SetActive(Equipped);


    }
}
