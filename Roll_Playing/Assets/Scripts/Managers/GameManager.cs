using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    #region 유일성을 보장 하는 Static 변수들
    static GameManager s_Instance;
    static GameManager Instance{  get { Init(); return s_Instance;} } // null Check 해주고 혹시 몰라서 초기화 해줌

    InputManager s_inputManager = new InputManager();
    ResourceManager s_resourceManger = new ResourceManager();
    public static InputManager Input { get { return Instance.s_inputManager; } }
    public static ResourceManager Resource { get { return Instance.s_resourceManger; } }

    #endregion

    #region Initialize
    static void Init()
    {
        if (s_Instance == null)
        {
            GameObject go = GameObject.Find("@GameManager");
            if (go == null)
            {
                go = new GameObject("@GameManager");
                go.AddComponent<GameManager>();
            }
            DontDestroyOnLoad(go);
            s_Instance = go.GetComponent<GameManager>();
        }
    }
    #endregion Initialize

    #region Unity Method
    void Start()
    {
        Init();
    }

    private void Update()
    {
        Input.OnUpdate();
    }

    #endregion Unity Method
       
}
