using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    /// <summary>
    /// Project�� �������� ��ɵ��� �����ϴ� Manager 
    /// �ش� Manager���� ���ϼ��� �����ϴ� Scene , Sound , Resources , Data , Input ���� ��ҵ��� ���� ���ش�.
    /// Input �̳� Resources�� �̰� �������� ȣ��Ǵ� ����. �̽�Ʈ��ŷ�� ��ư�. �Ѱ����� �����ϰ� 
    /// �̹� ���� �� �Լ��鵵 wrapping�Ͽ�. ������ ������ Managerȭ �����ִ°� ����.
    /// </summary>


    private static GameManager s_instance; // ���������� �ν��Ͻ�ȭ�� ����
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
