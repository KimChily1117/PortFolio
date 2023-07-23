using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;


public abstract class UI_Base : MonoBehaviour
{
    Dictionary<Type, UnityEngine.Object[]> _objects = new Dictionary<Type, UnityEngine.Object[]>();
    public enum Buttons 
    {
        PointButton
    }

    public enum Texts
    {
        PointText        
    }

    public enum Images
    {
        IconTest,  
    }
    
    protected void Bind<T>(Type type) where T : UnityEngine.Object
    {
        string[] names = Enum.GetNames(type);
        UnityEngine.Object[] objects = new UnityEngine.Object[names.Length];
        _objects.Add(typeof(T), objects);

        for (int i = 0; i < names.Length; i++)
        {
            if (typeof(T) == typeof(GameObject)) // when type gameObject --> Different Function
            {
                objects[i] = Util.FindChild(gameObject, names[i], true);
            }

            else
            {
                objects[i] = Util.FindChild<T>(gameObject, names[i], true);
            }

            if (objects[i] == null)
            {
                Debug.LogError($"Bind Error : index : {i} {names[i]}");
            }
        }
    }

    protected T Get<T>(int idx) where T : UnityEngine.Object
    {
        UnityEngine.Object[] objects = null;
        if (_objects.TryGetValue(typeof(T), out objects) == false) return null;

        return objects[idx] as T;
    }

    public static void BindEvent(GameObject go, Action<PointerEventData> action, Define.UIEvent type = Define.UIEvent.Click)
    {
        UI_EventHandler evt = Util.GetOrAddComponent<UI_EventHandler>(go);

        switch (type)
        {
            case Define.UIEvent.Click:
                evt.ClickEventAction -= action;
                evt.ClickEventAction += action;
                break;
            case Define.UIEvent.Drag:
                evt.DragEventAction -= action;
                evt.DragEventAction += action;
                break;
            default:
                Debug.Log($"BindEvent] type param error");
                break;
        }
    }

    public abstract void Init();


    

    private void Start()
    {
      
    }


    #region Getter
    protected GameObject GetGameObject(int idx) { return Get<GameObject>(idx); }
    protected Text GetText(int idx) { return Get<Text>(idx); }
    protected Button GetButton(int idx) { return Get<Button>(idx); }
    protected Image GetImage(int idx) { return Get<Image>(idx); }
    
    #endregion Getter



}
