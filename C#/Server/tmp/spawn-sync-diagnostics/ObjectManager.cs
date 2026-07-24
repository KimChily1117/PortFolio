using Character;
using Google.Protobuf.Protocol;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ObjectManager
{
    private const string DummyPrefix = "PD_Dummy";

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
        PositionInfo receivedPos = objectInfo.PosInfo;
        Debug.Log($"[SPAWN_SYNC][OBJECT_ADD_BEGIN] Scene={GameManager.SCENE.CurrentScene}, IsMyPlayer={isMyPlayer}, Type={gameObjectType}, ObjectId={objectInfo.ObjectId}, Name={objectInfo.Name}, Pos=({receivedPos?.PosX ?? 0f:0.00},{receivedPos?.PosY ?? 0f:0.00}), Existing={_objects.ContainsKey(objectInfo.ObjectId)}, CountBefore={_objects.Count}");

        if (gameObjectType == GameObjectType.Player)
        {
            if (isMyPlayer)
            {
                if (_objects.ContainsKey(objectInfo.ObjectId))
                {
                    Remove(objectInfo.ObjectId);
                }

                GameObject go = GameManager.Resources.Instantiate($"Character/male_ghostknight");
                go.AddComponent<MyPlayer>();

                go.name = objectInfo.Name;
                GameManager.MyName = objectInfo.Name;

                _objects.Add(objectInfo.ObjectId, go);

                MyPlayer = go.GetComponent<MyPlayer>();
                MyPlayer.Id = objectInfo.ObjectId;
                MyPlayer.ObjInfo = objectInfo;
                MyPlayer.PositionInfo = objectInfo.PosInfo;

                AttachNameLabel(go, objectInfo.Name, isMyPlayer: true);
                MyPlayer.SyncPos();
                Debug.Log($"[SPAWN_SYNC][OBJECT_ADD_APPLIED] Role=MyPlayer, ObjectId={MyPlayer.Id}, Name={go.name}, Active={go.activeSelf}, Transform=({go.transform.position.x:0.00},{go.transform.position.y:0.00},{go.transform.position.z:0.00}), CountAfter={_objects.Count}");
                LogCombatAnchorSnapshot("[CLIENT][SPAWN_ANCHOR][MY_PLAYER]", MyPlayer);
                TownScene.Current?.RegisterSpawnedCharacter(MyPlayer, isMyPlayer: true);
            }
            else
            {
                if (_objects.ContainsKey(objectInfo.ObjectId))
                {
                    Remove(objectInfo.ObjectId);
                }

                GameObject go = GameManager.Resources.Instantiate($"Character/other_male_ghostnight");

                go.AddComponent<OtherPlayer>();
                go.name = objectInfo.Name;

                _objects.Add(objectInfo.ObjectId, go);
                OtherPlayer Op = go.GetComponent<OtherPlayer>();
                Op.Id = objectInfo.ObjectId;
                Op.PositionInfo = objectInfo.PosInfo;
                Op.ObjInfo = objectInfo;

                AttachNameLabel(go, objectInfo.Name, isMyPlayer: false);
                go.AddComponent<RemotePlayerLod>();
                Op.SyncPos();
                Debug.Log($"[SPAWN_SYNC][OBJECT_ADD_APPLIED] Role=OtherPlayer, ObjectId={Op.Id}, Name={go.name}, Active={go.activeSelf}, Transform=({go.transform.position.x:0.00},{go.transform.position.y:0.00},{go.transform.position.z:0.00}), CountAfter={_objects.Count}");
                LogCombatAnchorSnapshot("[CLIENT][SPAWN_ANCHOR][OTHER_PLAYER]", Op);
                TownScene.Current?.RegisterSpawnedCharacter(Op, isMyPlayer: false);
            }
        }
        else if (gameObjectType == GameObjectType.Enemy)
        {
            if (_objects.ContainsKey(objectInfo.ObjectId))
            {
                Remove(objectInfo.ObjectId);
            }

            GameObject go = GameManager.Resources.Instantiate($"Enemy/enemy_Bakal");
            go.AddComponent<EnemyPlayer>();

            go.name = objectInfo.Name;

            _objects.Add(objectInfo.ObjectId, go);

            EnemyPlayer Ep = go.GetComponent<EnemyPlayer>();

            Ep.Id = objectInfo.ObjectId;
            Ep.PositionInfo = objectInfo.PosInfo;
            Ep.ObjInfo = objectInfo;

            Ep.SyncPos();
            Debug.Log($"[SPAWN_SYNC][OBJECT_ADD_APPLIED] Role=Enemy, ObjectId={Ep.Id}, Name={go.name}, Active={go.activeSelf}, Transform=({go.transform.position.x:0.00},{go.transform.position.y:0.00},{go.transform.position.z:0.00}), CountAfter={_objects.Count}");
            LogCombatAnchorSnapshot("[CLIENT][SPAWN_ANCHOR][ENEMY]", Ep);
        }
    }

    private void AttachNameLabel(GameObject owner, string playerName, bool isMyPlayer)
    {
        if (owner == null || string.IsNullOrEmpty(playerName))
            return;

        Transform oldLabel = owner.transform.Find("NameLabelCanvas");
        if (oldLabel != null)
            GameManager.Resources.Destroy(oldLabel.gameObject);

        bool isDummy = playerName.StartsWith(DummyPrefix, StringComparison.Ordinal);
        string displayName = isDummy ? $"[DUMMY] {playerName}" : $"[USER] {playerName}";
        Color labelColor = isDummy ? new Color(0.35f, 0.9f, 1f, 1f) : (isMyPlayer ? Color.white : new Color(1f, 0.25f, 0.25f, 1f));

        GameObject canvasObject = new GameObject("NameLabelCanvas");
        canvasObject.transform.SetParent(owner.transform, false);
        canvasObject.transform.localPosition = new Vector3(0f, 1.55f, 0f);
        canvasObject.transform.localScale = Vector3.one * 0.01f;

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingLayerName = "Charecter";
        canvas.sortingOrder = 100;

        CanvasScaler canvasScaler = canvasObject.AddComponent<CanvasScaler>();
        canvasScaler.dynamicPixelsPerUnit = 10f;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(260f, 48f);

        GameObject textObject = new GameObject("NameText");
        textObject.transform.SetParent(canvasObject.transform, false);

        TextMeshProUGUI nameText = textObject.AddComponent<TextMeshProUGUI>();
        nameText.text = displayName;
        nameText.alignment = TextAlignmentOptions.Center;
        nameText.fontSize = 22f;
        nameText.color = labelColor;
        nameText.raycastTarget = false;
        nameText.enableAutoSizing = true;
        nameText.fontSizeMin = 12f;
        nameText.fontSizeMax = 22f;
        nameText.outlineWidth = 0.15f;
        nameText.outlineColor = Color.black;

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
    }


    private void LogCombatAnchorSnapshot(string tag, BaseCharacter character)
    {
        if (character == null)
            return;

        Vector3 root = character.transform.position;
        Vector2 positionInfo = character.PositionInfo != null
            ? new Vector2(character.PositionInfo.PosX, character.PositionInfo.PosY)
            : Vector2.zero;

        string shadowPosition = "null";
        string shadowCollider = "null";
        if (character._shadowObject != null)
        {
            Vector3 shadow = character._shadowObject.transform.position;
            shadowPosition = FormatVector2(shadow);
            Collider2D shadowCol = character._shadowObject.GetComponent<Collider2D>();
            shadowCollider = FormatBounds(shadowCol);
        }

        Collider2D rootCollider = character.GetComponent<Collider2D>();
        SpriteRenderer spriteRenderer = character._Sprite != null ? character._Sprite.GetComponent<SpriteRenderer>() : character.GetComponentInChildren<SpriteRenderer>();

        Debug.Log($"{tag} Name={character.ObjInfo?.Name}, ObjectId={character.Id}, Root={FormatVector2(root)}, Shadow={shadowPosition}, RootCollider={FormatBounds(rootCollider)}, ShadowCollider={shadowCollider}, SpriteBounds={FormatSpriteBounds(spriteRenderer)}, PositionInfo={FormatVector2(positionInfo)}");
    }

    private string FormatVector2(Vector2 value)
    {
        return $"({value.x:0.00},{value.y:0.00})";
    }

    private string FormatBounds(Collider2D col)
    {
        if (col == null)
            return "null";

        Bounds b = col.bounds;
        return $"Center={FormatVector2(b.center)}, Min={FormatVector2(b.min)}, Max={FormatVector2(b.max)}";
    }

    private string FormatSpriteBounds(SpriteRenderer renderer)
    {
        if (renderer == null)
            return "null";

        Bounds b = renderer.bounds;
        return $"Center={FormatVector2(b.center)}, Min={FormatVector2(b.min)}, Max={FormatVector2(b.max)}";
    }
    public void Add(int id, GameObject go)
    {
        _objects.Add(id, go);
    }

    public void Remove(int id)
    {
        if (_objects.TryGetValue(id, out GameObject go) == false)
            return;

        _objects.Remove(id);

        if (go == null)
            return;

        MyPlayer myPlayer = go.GetComponent<MyPlayer>();
        if (myPlayer != null)
            myPlayer.UnbindInputForSceneChange();

        GameManager.Resources.Destroy(go);
    }

    public void RemoveRemoteObjects()
    {
        int myPlayerId = MyPlayer != null ? MyPlayer.Id : -1;
        List<int> removeIds = new List<int>();
        foreach (int id in _objects.Keys)
        {
            if (id != myPlayerId)
                removeIds.Add(id);
        }

        foreach (int id in removeIds)
        {
            Remove(id);
        }
    }
    public void RemoveMyPlayer()
    {
        if (MyPlayer == null)
            return;
        Remove(MyPlayer.Id);
        MyPlayer = null;
    }

    public void ForEachCharacter(Action<BaseCharacter> action)
    {
        if (action == null)
            return;

        foreach (GameObject obj in _objects.Values)
        {
            if (obj == null)
                continue;

            BaseCharacter character = obj.GetComponent<BaseCharacter>();
            if (character != null)
                action(character);
        }
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
        Debug.Log($"[SPAWN_SYNC][OBJECT_CLEAR_BEGIN] Scene={GameManager.SCENE.CurrentScene}, Count={_objects.Count}");
        foreach (KeyValuePair<int, GameObject> pair in _objects)
        {
            GameObject obj = pair.Value;
            Debug.Log($"[SPAWN_SYNC][OBJECT_CLEAR_ITEM] Scene={GameManager.SCENE.CurrentScene}, ObjectId={pair.Key}, Name={(obj != null ? obj.name : "null")}, Active={obj != null && obj.activeSelf}");
            MyPlayer myPlayer = obj != null ? obj.GetComponent<MyPlayer>() : null;
            if (myPlayer != null)
                myPlayer.UnbindInputForSceneChange();

            GameManager.Resources.Destroy(obj);
        }
        _objects.Clear();
        MyPlayer = null;
        Debug.Log($"[SPAWN_SYNC][OBJECT_CLEAR_END] Scene={GameManager.SCENE.CurrentScene}, Count={_objects.Count}");
    }
}






