using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 프로젝트 내의 리소스들을 Instantiate , Load , Destroy 하는 함수를 래핑한 매니저/// 
/// Resources 함수를 어디에서든 사용해서 인스턴스화를 해도 되긴한다./// 
/// 그러나 프로젝트 규모가 커지면 커질수록 한곳에서 관리를해서 추적하기 쉽게  
/// Manager화를 시켜주는게 좋다고한다. 상식적으로도 그게 맞다
/// </summary>
public class ResourceManager
{
    public T Load<T>(string path) where T : UnityEngine.Object // T(제네릭,템플릿)에는 뭐가 들어가는데? where? Object형식만 들어간다!
    {
        return Resources.Load<T>(path);
    }

    
    public GameObject Instantiate(string path , Transform parant=null)
    {
        GameObject prefab = Load<GameObject>($"Prefabs/{path}");
        if (prefab == null)
        {
            Debug.LogError($"filed to load prefab : {prefab}");
            return null;
        }

        return UnityEngine.Object.Instantiate(prefab, parant); //Object를 명시적으로 해준이유는 컴파일러가
        //자기 자신을 재귀함수로 계속 호출해주기 때문에 Object를 명시적으로 달아준다.
    }

    public void Destory(GameObject go)
    {
        if (go == null)
        {
            return;
        }

        UnityEngine.Object.Destroy(go);
    }


}
