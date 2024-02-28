using Character;
using Google.Protobuf.Protocol;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObjectManager
{
    public BaseCharacter MyPlayer { set; get; }
    Dictionary<int, GameObject> _objects = new Dictionary<int, GameObject>();

    public void Add(ObjectInfo playerInfo , bool isMyPlayer)
    {
        //if (_objects.ContainsKey(playerInfo.ObjectId) == true)
        //    return;

        if (isMyPlayer) 
        {
            // 이런식으로 리소스 매니저에서 받아온다.
            GameObject go = GameManager.Resources.Instantiate($"Character/male_ghostknight");
            go.AddComponent<MyPlayer>();

            go.name = playerInfo.Name;

            if (_objects.ContainsKey(playerInfo.ObjectId) == false)
            {
                _objects.Add(playerInfo.ObjectId, go);
            }


            MyPlayer = go.GetComponent<MyPlayer>();
            MyPlayer.Id = playerInfo.ObjectId;
            MyPlayer.ObjInfo = playerInfo;
            //MyPlayer.CellPos = new Vector2Int(playerInfo.PosInfo.PosX, playerInfo.PosInfo.PosY);
            MyPlayer.PosInfo = playerInfo.PosInfo;
            
        }

        else
        {
            GameObject go = GameManager.Resources.Instantiate($"Character/other_male_ghostnight");

            go.AddComponent<OtherPlayer>();
            go.name = playerInfo.Name;


            if (_objects.ContainsKey(playerInfo.ObjectId) == false)
            {
                _objects.Add(playerInfo.ObjectId, go);
            }
            OtherPlayer Op = go.GetComponent<OtherPlayer>();
            Op.Id = playerInfo.ObjectId;
            Op.PosInfo = playerInfo.PosInfo;
            Op.ObjInfo = playerInfo;
        }
    }





    public void Add(int id , GameObject go)
    {
        _objects.Add(id, go);   
    }


    public void Remove(int id) 
    {
        GameObject go = FindById(id);
        if (go == null)
            return;

        _objects.Remove(id);
        GameManager.Resources.Destroy(go);
    }

    public void RemoveMyPlayer()
    {
        if (MyPlayer == null)
            return;

        Remove(MyPlayer.Id);
        MyPlayer = null;
    }



    public GameObject FindById(int id)
    {
        GameObject value;

        _objects.TryGetValue(id, out value);

        return value;
    }

    public GameObject Find(Vector2Int cellPos)
    {
        foreach (GameObject obj in _objects.Values)
        {
            BaseCharacter bc = obj.GetComponent<BaseCharacter>();
            if (bc == null)
                continue;

            if (bc.CellPos == cellPos)
                return obj;
        }

        return null;
    }

    public GameObject Find(Func<GameObject, bool> condition)
    {
        foreach (GameObject obj in _objects.Values)
        {
            if (condition.Invoke(obj))
                return obj;
        }

        return null;
    }


    public void Clear()
    {
        foreach (GameObject obj in _objects.Values)
            GameManager.Resources.Destroy(obj);
        _objects.Clear();
    }
}
