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


    public static NetworkManager s_networkManager;

    public static NetworkManager Network 
    { 
        get 
        { 
            if(s_networkManager == null)
            {
                GameObject obj = GameObject.Find("@NetworkManager");

                if(!obj)
                {

                    obj = new GameObject { name = "@NetworkManager" };
                    obj.AddComponent<NetworkManager>();
                }
                s_networkManager = obj.GetComponent<NetworkManager>();
                DontDestroyOnLoad(obj);
            }
            return s_networkManager;         
        }    
    
    }


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


    private void Update() // update함수를 하나로만 써서 하나의 lifecycle로만 관리한다.
    {
        
    }
}
