using Data;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

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

    #region Contents

    private static InputManager s_input = new InputManager();
    public static InputManager Input { get { return s_input; } }


    private static NetworkManager s_networkmanager = new NetworkManager();
    public static NetworkManager Network { get { return s_networkmanager; } }


    private static InventoryManager s_inventorymanager = new InventoryManager();

    public static InventoryManager Inven { get { return s_inventorymanager; } }

    public static string MyName = "";


    #endregion Contents

    #region Core

    private static ResourcesManager s_resources = new ResourcesManager();
    public static ResourcesManager Resources { get { return s_resources; } }

    private static UIManager s_uimanager = new UIManager();
    public static UIManager UI { get { return s_uimanager; } }

    private static SceneManagers s_scenemanagers;
    public static SceneManagers SCENE { get { return s_scenemanagers; } }

    private static SoundManager s_soundmanagers = new SoundManager();
    public static SoundManager Sound { get { return s_soundmanagers; } }

    private static PoolManager s_poolmanagers = new PoolManager();
    public static PoolManager ObjectPool { get { return s_poolmanagers; } }

    private static ObjectManager s_objectmanager = new ObjectManager();
    public static ObjectManager ObjectManager { get { return s_objectmanager; } }

    private static DataManager s_dataManager = new DataManager();
    public static DataManager DataManager { get { return s_dataManager; } }


    #endregion Core



    /*
     
     
     decisionPlatform
     
     
     
     
     */

    private static Enums.PlatformType _playerplatformType = Enums.PlatformType.NONE;
    public static Enums.PlatformType PlayerPlatformType { get { return _playerplatformType; } } 

    public static void AssignPlatform()
    {
        if(Application.isMobilePlatform) 
        {
            _playerplatformType = Enums.PlatformType.MOBILE;
        }
        else
        {
            _playerplatformType = Enums.PlatformType.DESKTOP;
        }
    }


    public static string GetLoginUniqueId()
    {
        string uniqueIdOverride = GetCommandLineValue("-uniqueIdOverride=");
        if (!string.IsNullOrWhiteSpace(uniqueIdOverride))
            return uniqueIdOverride.Trim();

        string testClientId = GetCommandLineValue("-testClientId=");
        if (!string.IsNullOrWhiteSpace(testClientId))
            return $"{SystemInfo.deviceUniqueIdentifier}_{testClientId.Trim()}";

        return SystemInfo.deviceUniqueIdentifier;
    }

    private static string GetCommandLineValue(string prefix)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (!arg.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            return arg.Substring(prefix.Length);
        }

        return null;
    }




    static void Init()
    {
        AssignPlatform();
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


            GameObject SceneMgr = new GameObject { name = "@SceneMgr" };
            s_scenemanagers = SceneMgr.GetOrAddComponent<SceneManagers>();
            SceneMgr.transform.SetParent(obj.transform);
        }

        Sound.Init();

        DataManager.Init();
        EnsureSingleEventSystem();
        Application.runInBackground = true;
        Application.targetFrameRate = 300;

        Screen.SetResolution(800, 600, false);

        UI.PreloadToast();
    }


    private static void EnsureSingleEventSystem()
    {
        EventSystem[] eventSystems = GameObject.FindObjectsOfType<EventSystem>(true);
        EventSystem keep = null;

        foreach (EventSystem eventSystem in eventSystems)
        {
            if (eventSystem == null)
                continue;

            if (keep == null)
            {
                keep = eventSystem;
                continue;
            }

            Destroy(eventSystem.gameObject);
        }

        if (keep == null)
        {
            GameObject evt = Resources.Instantiate("UI/EventSystem");
            keep = evt != null ? evt.GetComponent<EventSystem>() : null;
        }

        if (keep != null)
        {
            keep.gameObject.name = "EventSystem";
            DontDestroyOnLoad(keep.gameObject);
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureSingleEventSystem();
    }
    void OnEnable()
    {

        Init();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        //StartCoroutine(InitializeNetwork());

        Network.apiHelper.MakeCharInfoURL();


        StartCoroutine(RequestGetData(Network.apiHelper.TEST_URL, (cbMessage) =>
        {
            DataManager.CharDict = DataManager.LoadServerJson<Data.ApiCharInfo, string, Data.CharInfo>
                 (cbMessage).MakeDict();

            StartCoroutine(RequestGetData(Network.apiHelper.MakeCharDetailInfo(), (cbMessage) =>
            {
                Debug.Log($"Detail INFO Callback Test : {cbMessage}");
                DataManager.DetailInfos.Add(DataManager.LoadServerJson<Data.CharDetailInfo>(cbMessage));
            }));
        }));


        //Push(RequestGetData(Network.apiHelper.TEST_URL, (cbMessage) =>
        //{
        //    Debug.Log($"ddddd : {cbMessage}");

        //    DataManager.CharDict = DataManager.LoadServerJson<Data.ApiCharInfo, string, Data.CharInfo>
        //         (cbMessage).MakeDict();
        //}));



        //Push(RequestGetData(Network.apiHelper.MakeCharDetailInfo(), (cbMessage) =>
        //{
        //    Debug.Log($"Detail INFO Callback Test : {cbMessage}");
        //}));
    }


    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Update()
    {
        s_input.OnUpdate();
        s_networkmanager.OnUpdate();
    }


    #region ServerResponse 


    IEnumerator RequestGetData(string URL, Action<string> cbAction)
    {
        UnityWebRequest www = UnityWebRequest.Get(URL);
        yield return www.SendWebRequest();

        if (www.error == null)
        {
            cbAction.Invoke(www.downloadHandler.text);
        }
        yield return null;

    }

    #endregion

    #region Test


    public Queue<IEnumerator> coroutineQueue = new Queue<IEnumerator>();

    bool _isRunning;


    public void Push(IEnumerator _routine)
    {
        bool isRunning = false;

        coroutineQueue.Enqueue(_routine);

        if (_isRunning == false && coroutineQueue.Count >= 2)
        {
            isRunning = _isRunning = true;
        }

        if (isRunning)
            Flush();


    }


    private void Flush()
    {
        while (coroutineQueue.Count != 0)
        {

            IEnumerator routine = Pop();
            if (routine == null)
                return;

            StartCoroutine(FlushCoroutine(routine));

        }
    }

    private IEnumerator Pop()
    {
        Debug.Log($"Count ? {coroutineQueue.Count}");

        if (coroutineQueue.Count == 0)
        {
            return null;
        }

        return coroutineQueue.Dequeue();
    }


    private IEnumerator FlushCoroutine(IEnumerator routine)
    {
        Debug.Log($"routine differnt? : {routine.GetHashCode()}");
        yield return StartCoroutine(routine);
    }

    #endregion
}


