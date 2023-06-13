#include <iostream>
using namespace std;
#include "Item.h"
#include "Inventory.h"

// [시스템] 몬스터를 잡을 때, 아이템을 떨군다
Item* DropItem()
{
    if (rand() % 2 == 0)
    {
        Weapon* weapon = new Weapon();
        return weapon;
    }
    else
    {
        Armor* armor = new Armor();
        return armor;
    }
}

int main()
{
    srand((unsigned)time(0));

    // 몬스터 처치
    for (int i = 0; i < 100; i++)
    {
        Item* item = DropItem();
        item->PrintInfo();

        if (Inventory::GetInstance()->AddItem(item))
        {
            cout << "Added Item to Inven" << endl;
        }
        else
        {
            cout << "Failed to add Item" << endl;
            delete item;
        }
    }

    // PK 당해서 랜덤으로 일부 아이템 드랍
    for (int i = 0; i < 20; i++)
    {
        int randIndex = rand() % MAX_SLOT;
        Item* item = Inventory::GetInstance()->GetItemAtSlot(randIndex);
        if (item && Inventory::GetInstance()->RemoveItem(item))
        {
            cout << "Removed Item" << endl;
        }
    }
}