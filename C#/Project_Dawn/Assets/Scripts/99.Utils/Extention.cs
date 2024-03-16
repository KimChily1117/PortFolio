using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public static class Extention
{  
    public static void BindEvent(this GameObject go , Action<PointerEventData> action , Define.UIEvent type = Define.UIEvent.Click)
    {
        Debug.Log($"event binding : {go}");
        UI_Base.BindEvent(go, action, type);
    }

    public static T GetOrAddComponent<T>(this GameObject go) where T : UnityEngine.Component
    {
        return Util.GetOrAddComponent<T>(go);
    }

    public static T FindChild<T>(this GameObject go, string name = null, bool reculsive = false) where T : UnityEngine.Object
    {
        return Util.FindChild<T>(go,name,reculsive);
    }
}
