using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class ResourcesManager
{
    public T Load<T>(string path) where T : UnityEngine.Object
    {
        return Resources.Load<T>(path);
    }

    public GameObject Instantiate(string path,Transform parent = null)
    {
        GameObject prefab = Load<GameObject>($"Prefabs/{path}");
        if (prefab == null)
        {
            Debug.Log($"Filed to load prefab : {path}");
            return null;
        }

        GameObject go = UnityEngine.Object.Instantiate(prefab, parent);

        int index = go.name.IndexOf("(Clone)");
        if(index > 0) // Clone(Prefab을 인스턴스화 하면 생김)
        {
          go.name = go.name.Substring(0,index);
        }
        return go;
    }
    
    public void Destroy(GameObject go)
    {
        if (go == null)
            return;
        UnityEngine.Object.Destroy(go);
    }

}
