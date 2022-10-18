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

    private static InputManager s_input = new InputManager();
    public static InputManager Input { get { return s_input; } }

    private static ResourcesManager s_resources = new ResourcesManager();
    public static ResourcesManager Resources { get { return s_resources; } }

    private static UIManager s_uimanager = new UIManager();
    public static UIManager UI { get { return s_uimanager; } }

    private static SceneManangers s_scenemanagers = new SceneManangers();
    public static SceneManangers SCENE { get { return s_scenemanagers; } }
    
    private static SoundManager s_soundmanagers = new SoundManager();
    public static SoundManager Sound { get { return s_soundmanagers; } }

    private static PoolManager s_poolmanagers = new PoolManager();
    
    public static PoolManager ObjectPool  { get { return s_poolmanagers; }}

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
        Sound.Init();
        Sound.Play("Sounds/town_bgm_elven",Define.SoundType.BGM);
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