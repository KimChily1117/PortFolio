using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    private static GameManager s_instance;
    private static GameManager Instance// Property
    {
        get
        {
            Init();
            return s_instance;
        }
    }
    
    
    
    static InputManager s_input = new InputManager();
    public static InputManager Input { get { return s_input; } }

    static ResourcesManager s_resources = new ResourcesManager();
    public static ResourcesManager Resources { get { return s_resources; } }

    static UIManager s_uimanager = new UIManager();
    public static UIManager UI { get { return s_uimanager; } }

    static SceneManangers s_scenemanagers = new SceneManangers();

    public static SceneManangers SCENE { get { return s_scenemanagers; } }

    static void Init()
    {
        if (!s_instance)
        {
            GameObject obj = GameObject.Find("@Managers");
            if (!obj)
            {
                obj = new GameObject { name = "@Managers" };
                obj.AddComponent<GameManager>();
            }
            DontDestroyOnLoad(obj);

            s_instance = obj.GetComponent<GameManager>();
        }        
    }

    void Start()
    {
        Init();
    }

    private void Update()
    {
        s_input.OnUpdate();
    }

}
