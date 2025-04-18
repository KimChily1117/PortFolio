using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Util
{
    public static GameObject FindChild(GameObject go, string name = null, bool recursive = false) 
    {

        Debug.Log($"go : {go} , name : {name}");

        Transform transform = FindChild<Transform>(go,name,recursive);
        if(transform != null)
        {
            return transform.gameObject;
        }

        return null;
    }

    public static T FindChild<T>(GameObject go, string name = null, bool recursive = false) where T : UnityEngine.Object
    {

        Debug.Log($"go : {go} , name : {name}");
        if (go == null) return null;

        if (!recursive)
        {
            for (int i = 0; i < go.transform.childCount; i++)
            {
                Transform transform = go.transform.GetChild(i);
                if (string.IsNullOrEmpty(name) || transform.name == name)
                {
                    T component = transform.GetComponent<T>();
             
             
                    if (component) return component;
                }
            }
        }

        else
        {
            foreach (T component in go.GetComponentsInChildren<T>())
            {
                if (string.IsNullOrEmpty(name) || component.name == name)
                {
                    Debug.Log($"KSY ]  {component.name} , {name}");
                    return component;
                }
            }
            

        }


        return null;
    }

    public static T GetOrAddComponent<T>(GameObject go) where T : UnityEngine.Component
    {
        T component = go.GetComponent<T>();

        if (component == null) { component = go.AddComponent<T>(); }

        return component;
    }


}
