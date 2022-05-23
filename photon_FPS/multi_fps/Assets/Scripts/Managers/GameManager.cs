using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    private static GameManager s_instance;
    private static GameManager Instance 
    {              
        get
        {
            Init();
            return s_instance;
        } 
    }


    static NetworkManager s_networkManager = new NetworkManager();

    public static NetworkManager Network { get { return s_networkManager; } }


    static void Init()
    {
        if (!s_instance)
        {
            GameObject obj = GameObject.Find("@GameManager");   

            if(!obj)
            {
                //GameObject.in
                obj = new GameObject { name = "@GameManager" }; 
                obj.AddComponent<GameManager>();
            }

            s_instance = obj.GetComponent<GameManager>();
            DontDestroyOnLoad(obj);
        }
    }

    void Start()
    {
        Init();
    }


    private void Update() // update�Լ��� �ϳ��θ� �Ἥ �ϳ��� lifecycle�θ� �����Ѵ�.
    {
        
    }
}
