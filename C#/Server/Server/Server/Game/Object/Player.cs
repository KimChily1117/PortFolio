using Google.Protobuf.Protocol;
using Server.DB;
using Server.Game.Room;
using System;
using System.Collections.Generic;
using System.Text;

namespace Server.Game.Object
{
    public class Player : GameObject
    {
        public int PlayerDbId { get; set; }
        public ClientSession Session { get; set; }

        public int WeaponDamage { get; private set; }
        public int ArmorDefence { get; private set; }


        public int TotalDamage { get { return 10 + WeaponDamage; } }

        public int TotalDefence { get { return ArmorDefence; } }


        public Inventory Inven { get; private set; } = new Inventory();
        public Player()
        {
            ObjectType = GameObjectType.Player;
            CurrentPlayerState = PlayerState.Idle;
            HP = 100;
        }

        public override void OnDead(GameObject attacker)
        {
            S_Die s_Die = new S_Die();
            s_Die.Player = Info;
            Room.Broadcast(s_Die);

            //GameRoom room = Room;
            //room.LeaveRoom(Id);
        }



        public void HandleEquipItem(C_EquipItem equipPacket)
        {
            Item item = Inven.Get(equipPacket.ItemDbId);
            // 일단 Item을 Get해옴

            if (item.ItemType == ItemType.Consumable)
            {
                Console.WriteLine($"소모품입니다. 돌아가세요 (아직 미구현)");
                return;
            }

            // 착용 요청이라면, 겹치는 부위 해제
            if (equipPacket.Equipped)
            {
                Item unequipItem = null;

                switch (item.ItemType)
                {
                    case ItemType.Weapon:
                        unequipItem = Inven.Find(i=>i.Equipped && i.ItemType == ItemType.Weapon);
                        break;

                    case ItemType.Armor:
                        ArmorType armorType = ((Armor)item).ArmorType;

                        unequipItem = Inven.Find(i => i.Equipped && 
                        i.ItemType == ItemType.Armor &&
                        ((Armor)i).ArmorType == armorType);
                        break;
                }

                if(unequipItem != null) 
                {
                    // 메모리 선적용
                    unequipItem.Equipped = false;

                    // DB에 Noti
                    DbTransaction.EquipItemNoti(this, unequipItem);

                    // 클라에 통보
                    S_EquipItem equipOkItem = new S_EquipItem();
                    equipOkItem.ItemDbId = unequipItem.ItemDbId;
                    equipOkItem.Equipped = unequipItem.Equipped;
                    Session.Send(equipOkItem);
                }
            }

            
            {
                // 메모리 선적용
                item.Equipped = equipPacket.Equipped;

                // DB에 Noti
                DbTransaction.EquipItemNoti(this, item);

                // 클라에 통보
                S_EquipItem equipOkItem = new S_EquipItem();
                equipOkItem.ItemDbId = equipPacket.ItemDbId;
                equipOkItem.Equipped = equipPacket.Equipped;
                Session.Send(equipOkItem);
            }
            RefreshAdditionalStat();
        }


        public void RefreshAdditionalStat()
        {
            WeaponDamage = 0;
            ArmorDefence = 0;

            foreach (Item item in Inven.Items.Values)
            {
                if (item.Equipped == false)
                    continue;

                switch (item.ItemType)
                {
                    case ItemType.Weapon:
                        WeaponDamage += ((Weapon)item).Damage;
                        break;
                    case ItemType.Armor:
                        ArmorDefence += ((Armor)item).Defence;
                        break;
                }
            }
        }

    }
}
