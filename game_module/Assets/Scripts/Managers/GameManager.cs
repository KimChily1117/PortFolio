using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    /// <summary>
    /// Project에 전반적인 기능들을 제어하는 Manager 
    /// 해당 Manager에서 단일성을 보장하는 Scene , Sound , Resources , Data , Input 등의 요소들을 관리 해준다.
    /// Input 이나 Resources가 이곳 저곳에서 호출되는 순간. 이슈트래킹이 어렵고. 한곳에서 관리하고 
    /// 이미 정의 된 함수들도 wrapping하여. 추적이 쉽도록 Manager화 시켜주는게 좋다.
    /// </summary>


    private static GameManager s_instance; // 실질적으로 인스턴스화된 변수
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


    // Start is called before the first frame update
    void Start()
    {
        Init();
    }

    private void Update()
    {
        s_input.OnUpdate();
    }

}
