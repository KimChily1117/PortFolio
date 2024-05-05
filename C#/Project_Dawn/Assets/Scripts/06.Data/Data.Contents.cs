using Google.Protobuf.Protocol;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Data
{
    #region Stat
    [Serializable]
    public class Stat
    {
        public int level;
        public int maxHp;
        public int attack;
        public int totalExp;
    }

    [Serializable]
    public class StatData : ILoader<int, Stat>
    {
        public List<Stat> stats = new List<Stat>();

        public Dictionary<int, Stat> MakeDict()
        {
            Dictionary<int, Stat> dict = new Dictionary<int, Stat>();
            foreach (Stat stat in stats)
                dict.Add(stat.level, stat);
            return dict;
        }
    }
    #endregion
    #region Skill
    [Serializable]
    public class Skill
    {
        public int id;
        public string name;
        public float cooldown;
        public int damage;
        //public SkillType skillType;
        public ProjectileInfo projectile;
    }

    public class ProjectileInfo
    {
        public string name;
        public float speed;
        public int range;
        public string prefab;
    }

    [Serializable]
    public class SkillData : ILoader<int, Skill>
    {
        public List<Skill> skills = new List<Skill>();

        public Dictionary<int, Skill> MakeDict()
        {
            Dictionary<int, Skill> dict = new Dictionary<int, Skill>();
            foreach (Skill skill in skills)
                dict.Add(skill.id, skill);
            return dict;
        }
    }
    #endregion

    #region Item
    [Serializable]
    public class ItemData
    {
        public int id;
        public string name;
        public ItemType itemType;
        public string iconPath;
    }

    [Serializable]
    public class WeaponData : ItemData
    {
        public WeaponType weaponType;
        public int damage;
    }

    [Serializable]
    public class ArmorData : ItemData
    {
        public ArmorType armorType;
        public int defence;
    }

    [Serializable]
    public class ConsumableData : ItemData
    {
        public ConsumableType consumableType;
        public int maxCount;
    }


    [Serializable]
    public class ItemLoader : ILoader<int, ItemData>
    {
        public List<WeaponData> weapons = new List<WeaponData>();
        public List<ArmorData> armors = new List<ArmorData>();
        public List<ConsumableData> consumables = new List<ConsumableData>();

        public Dictionary<int, ItemData> MakeDict()
        {
            Dictionary<int, ItemData> dict = new Dictionary<int, ItemData>();
            foreach (ItemData item in weapons)
            {
                item.itemType = ItemType.Weapon;
                dict.Add(item.id, item);
            }
            foreach (ItemData item in armors)
            {
                item.itemType = ItemType.Armor;
                dict.Add(item.id, item);
            }
            foreach (ItemData item in consumables)
            {
                item.itemType = ItemType.Consumable;
                dict.Add(item.id, item);
            }
            return dict;
        }
    }
    #endregion

    #region Monster

    [Serializable]
    public class MonsterData
    {
        public int id;
        public string name;
        public StatInfo stat;
        public string prefabPath;
    }

    [Serializable]
    public class MonsterLoader : ILoader<int, MonsterData>
    {
        public List<MonsterData> monsters = new List<MonsterData>();

        public Dictionary<int, MonsterData> MakeDict()
        {
            Dictionary<int, MonsterData> dict = new Dictionary<int, MonsterData>();
            foreach (MonsterData monster in monsters)
            {
                dict.Add(monster.id, monster);
            }
            return dict;
        }
    }

    #endregion

    #region API_Character_info(NEOPLE)

    [System.Serializable]
    public class ApiCharInfo : ILoader<string, CharInfo>
    {
        public List<CharInfo> rows = new List<CharInfo>();
        public Dictionary<string, CharInfo> MakeDict()
        {
            Dictionary<string, CharInfo> dict = new Dictionary<string, CharInfo>();

            GameManager.DataManager.CHARID = rows[0].characterId;

            foreach (CharInfo info in rows)
            {
                dict.Add(info.characterId, info);
            }
            return dict;
        }
    }


    [System.Serializable]
    public class ApiCharDetailInfo : ILoader<string, CharDetailInfo>
    {
        public Dictionary<string, CharDetailInfo> MakeDict()
        {
            Dictionary<string, CharDetailInfo> dict = new Dictionary<string, CharDetailInfo>();

            //foreach (CharDetailInfo info in rows)
            //{
            //    dict.Add(info.characterId, info);
            //}
            return dict;
        }
    }




    [System.Serializable]
    public class CharInfo
    {
        public string serverId;
        public string characterId;
        public string characterName;
        public int level;
        public string jobId;
        public string jobGrowId;
        public string jobName;
        public string jobGrowName;
        public int fame;
    }


    [System.Serializable]
    public class CharDetailInfo
    {
        public string characterId;
        public string characterName;
        public int level;
        public string jobId;
        public string jobGrowId;
        public string jobName;
        public string jobGrowName;
        public string adventureName;
        public string guildId;
        public string guildName;
    }


    #endregion

}