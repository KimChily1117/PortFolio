using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ������Ʈ ���� ���ҽ����� Instantiate , Load , Destroy �ϴ� �Լ��� ������ �Ŵ���/// 
/// Resources �Լ��� ��𿡼��� ����ؼ� �ν��Ͻ�ȭ�� �ص� �Ǳ��Ѵ�./// 
/// �׷��� ������Ʈ �Ը� Ŀ���� Ŀ������ �Ѱ����� �������ؼ� �����ϱ� ����  
/// Managerȭ�� �����ִ°� ���ٰ��Ѵ�. ��������ε� �װ� �´�
/// </summary>
public class ResourceManager
{
    public T Load<T>(string path) where T : UnityEngine.Object // T(���׸�,���ø�)���� ���� ���µ�? where? Object���ĸ� ����!
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

        return UnityEngine.Object.Instantiate(prefab, parant); //Object�� ��������� ���������� �����Ϸ���
        //�ڱ� �ڽ��� ����Լ��� ��� ȣ�����ֱ� ������ Object�� ��������� �޾��ش�.
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
