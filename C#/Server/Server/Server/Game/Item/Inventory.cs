﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Server.Game
{
    public class Inventory
    {
        public Dictionary<int, Item> Items = new Dictionary<int, Item>();

        public void Add(Item item)
        {
            Items.Add(item.ItemDbId, item);
        }

        public Item Get(int itemDbId)
        {
            Item item = null;
            Items.TryGetValue(itemDbId, out item);
            return item;
        }

        public Item Find(Func<Item, bool> condition)
        {
            foreach (Item item in Items.Values)
            {
                if (condition.Invoke(item))
                    return item;
            }

            return null;
        }

        public int? GetEmptySlot()
        {
            for (int slot = 0; slot < 20; slot++)
            {
                Item item = Items.Values.FirstOrDefault(i => i.Slot == slot);

                if (item == null)
                    return slot;
            }

            return null;
        }
    }
}
