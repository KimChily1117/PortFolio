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

    public static GameObjectType GetObjectTypeId(int id)
    {
        int type = (id >> 24) & 0x7F;
        return (GameObjectType)type;
    }



    public void Add(ObjectInfo objectInfo, bool isMyPlayer)
    {
        GameObjectType gameObjectType = GetObjectTypeId(objectInfo.ObjectId);
        
        
        if (gameObjectType == GameObjectType.Player)
        {

            if (isMyPlayer)
            {
                // 이런식으로 리소스 매니저에서 받아온다.
                GameObject go = GameManager.Resources.Instantiate($"Character/male_ghostknight");
                go.AddComponent<MyPlayer>();

                go.name = objectInfo.Name;
                GameManager.MyName = objectInfo.Name;


                if (_objects.ContainsKey(objectInfo.ObjectId))
                {
                    _objects.Remove(objectInfo.ObjectId);
                }
                _objects.Add(objectInfo.ObjectId, go);

                MyPlayer = go.GetComponent<MyPlayer>();
                MyPlayer.Id = objectInfo.ObjectId;
                MyPlayer.ObjInfo = objectInfo;
                //MyPlayer.CellPos = new Vector2Int(playerInfo.PosInfo.PosX, playerInfo.PosInfo.PosY);
                MyPlayer.PositionInfo = objectInfo.PosInfo;


                MyPlayer.SyncPos();
            }

            else
            {
                
                
                GameObject go = GameManager.Resources.Instantiate($"Character/other_male_ghostnight");

                go.AddComponent<OtherPlayer>();
                go.name = objectInfo.Name;


                if (_objects.ContainsKey(objectInfo.ObjectId))
                {
                    _objects.Remove(objectInfo.ObjectId);
                    Remove(objectInfo.ObjectId);
                }

                _objects.Add(objectInfo.ObjectId, go);
                OtherPlayer Op = go.GetComponent<OtherPlayer>();
                Op.Id = objectInfo.ObjectId;
                Op.PositionInfo = objectInfo.PosInfo;
                Op.ObjInfo = objectInfo;

                Op.SyncPos();
            }
        }

        else if (gameObjectType == GameObjectType.Enemy)
        {
            GameObject go = GameManager.Resources.Instantiate($"Enemy/enemy_hismar");
            go.AddComponent<EnemyPlayer>();

            go.name = objectInfo.Name;


            if (_objects.ContainsKey(objectInfo.ObjectId))
            {
                _objects.Remove(objectInfo.ObjectId);
            }

            _objects.Add(objectInfo.ObjectId, go);

            EnemyPlayer Ep = go.GetComponent<EnemyPlayer>();

            Ep.Id = objectInfo.ObjectId;
            Ep.PositionInfo = objectInfo.PosInfo;
            Ep.ObjInfo = objectInfo;

            Ep.SyncPos();
        }
    }





    public void Add(int id, GameObject go)
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
