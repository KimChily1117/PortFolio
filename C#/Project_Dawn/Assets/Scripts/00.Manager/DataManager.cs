using System.Collections.Generic;
using UnityEngine;
using Data;

public interface ILoader<Key, Value>
{
    Dictionary<Key, Value> MakeDict();
}

public class DataManager
{
    //public Dictionary<int, Data.Stat> StatDict { get; private set; } = new Dictionary<int, Data.Stat>();
    //public Dictionary<int, Data.Skill> SkillDict { get; private set; } = new Dictionary<int, Data.Skill>();

    public Dictionary<string, Data.CharInfo> CharDict { get; set; } = new Dictionary<string, CharInfo>();
    public Dictionary<string, Data.CharDetailInfo> DetailCharDict { get; set; } = new Dictionary<string, CharDetailInfo>();

    public List<Data.CharDetailInfo> DetailInfos { get; set; } = new List<CharDetailInfo>();


    public string CHARID { get; set; }



    public void Init()
    {
        TextAsset textAsset = GameManager.Resources.Load<TextAsset>($"Data/StatData");

        Debug.Log($"? : {textAsset.text}");

        //StatDict = LoadJson<Data.StatData, int, Data.Stat>("StatData").MakeDict();
        //SkillDict = LoadJson<Data.SkillData, int, Data.Skill>("SkillData").MakeDict();
        //CharDict = LoadJson<Data.ApiCharInfo, string, Data.CharInfo>("NeopleData").MakeDict();

    }


    /// <summary>
    /// Local�� �ִ� Json���ϵ��� Parsing���ִ� �Լ�
    /// </summary>
    /// <typeparam name="Loader"></typeparam>
    /// <typeparam name="Key"></typeparam>
    /// <typeparam name="Value"></typeparam>
    /// <param name="filepath">���� ���</param>
    /// <returns></returns>
    Loader LoadJson<Loader, Key, Value>(string filepath) where Loader : ILoader<Key, Value>
    {
        TextAsset textAsset = GameManager.Resources.Load<TextAsset>($"Data/{filepath}");
        return JsonUtility.FromJson<Loader>(textAsset.text);
    }

    
    /// <summary>
    /// �������� ���� ���� Json Format ���䰪�� Parsing���ִ� �Լ�
    /// </summary>
    /// <typeparam name="Loader"></typeparam>
    /// <typeparam name="Key"></typeparam>
    /// <typeparam name="Value"></typeparam>
    /// <param name="responseJson">�������� ���� ��</param>
    /// <returns></returns>
    public Loader LoadServerJson<Loader, Key, Value>(string responseJson) where Loader : ILoader<Key, Value>
    {
        Debug.Log($"??? : {responseJson}");
        return JsonUtility.FromJson<Loader>(responseJson);
    }


    public Loader LoadServerJson<Loader>(string responseJson)
    {
        return JsonUtility.FromJson<Loader>(responseJson);
    }


}