using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class UI_Inventory : UI_PopUp
{
    public List<UI_Inven_Item> Items { get; } = new List<UI_Inven_Item>();

    private void Awake()
    {
        Init();
    }


    public override void Init()
    {
        base.Init();

        Items.Clear();

        GameObject grid = transform.Find("ItemGrid").gameObject;
        foreach (Transform child in grid.transform)
            Destroy(child.gameObject);

        for (int i = 0; i < 20; i++)
        {
            GameObject go = GameManager.Resources.Instantiate("UI/PopUp/UI_InvenItem", grid.transform);
            UI_Inven_Item item = go.GetOrAddComponent<UI_Inven_Item>();
            Items.Add(item);
        }
    }

    public void RefreshUI()
    {
        List<Item> items = GameManager.Inven.Items.Values.ToList();
        items.Sort((left, right) => { return left.Slot - right.Slot; });

        foreach (Item item in items)
        {
            if (item.Slot < 0 || item.Slot >= 20)
                continue;

            Items[item.Slot].SetItem(item);
        }
    }
}
