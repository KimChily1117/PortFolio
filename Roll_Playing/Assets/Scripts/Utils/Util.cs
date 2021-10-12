using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Util
{

    public static T FindChild<T>(GameObject gameObject , string name = null , bool isRecursive = false) where T : UnityEngine.Object
    {
        if (gameObject == null)
        {
            return null;
        }

        if (isRecursive == false) // ���ȣ���� false�� �������� ��.
        {
            for (int i = 0; i < gameObject.transform.childCount; i++)
            {
                Transform transform = gameObject.transform.GetChild(i);
                if (string.IsNullOrEmpty(name) || transform.name == name)
                {
                    T component = transform.GetComponent<T>();
                    if (component != null)
                        return component;
                }
            }
        }

        else
        {
            foreach (T component in gameObject.GetComponentsInChildren<T>())
            {
                if (string.IsNullOrEmpty(name) || component.name == name)
                { return component; }
            }
        }

        return null;
    }

}
