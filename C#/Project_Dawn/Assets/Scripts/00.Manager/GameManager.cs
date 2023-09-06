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

    private static NetworkManager s_networkmanager = new NetworkManager();

    public static NetworkManager Network { get { return s_networkmanager; } }


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
        Sound.Play($"Sounds/CharacterSelect",Define.SoundType.BGM);
        // 수정 해야 할 수도 있음, 그러나 게임매니저 초기화는 무조건 초기(로그인)화면에서 진행되기때문에 상관없음 23.07.14
        
    }
    void Start()
    {
        Init();
    }
    private void Update()
    {
        s_input.OnUpdate();
        s_networkmanager.OnUpdate();
    }
    
}