using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.UI.CanvasScaler;


public class UIController
{

}

// UI??sort orderÎ•?
public class UIManager
{
    public int _order = 10;
    Stack<UI_PopUp> _popupStack = new Stack<UI_PopUp>();
    ToastPopup _toastPopup;
    UI_ComboCounter _comboCounter;

    public UI_Scene _scene { private set; get; }

    public GameObject Root
    {
        get
        {
            GameObject root = GameObject.Find("@UI_Root");

            if (root == null)
            {
                root = new GameObject { name = "@UI_Root" };
            }

            return root;
        }
    }


    public void SetCanvas(bool sort = true)
    {
        Canvas canvas = Util.GetOrAddComponent<Canvas>(Root);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        Util.GetOrAddComponent<GraphicRaycaster>(Root);

        Root.transform.position = Vector3.zero;

        CanvasScaler sc = Util.GetOrAddComponent<CanvasScaler>(Root);

        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920,1080);
        sc.screenMatchMode = ScreenMatchMode.MatchWidthOrHeight;



        canvas.overrideSorting = true;
        if (sort) //?ùÏóÖ?¥Îûë Í¥Ä???àÎäî Sorting???ÑÏöî??UI
        {
            canvas.sortingOrder = _order;
            _order++;
        }
        else
        {
            canvas.sortingOrder = 0;
        }




        //go.transform.SetParent(Root.transform);
    }


    public T ShowPopupUI<T>(string name = null) where T : UI_PopUp
    {
        if (string.IsNullOrEmpty(name) == true)
        {
            Debug.Log($"Param is Null or Empty ");
            Debug.Log($"Type of Generic T {typeof(T).Name} ");
            name = typeof(T).Name;
        }
        string path = $"UI/PopUp/{name}";

        GameObject go = GameManager.Resources.Instantiate(path);

        T popup = Util.GetOrAddComponent<T>(go);

        _popupStack.Push(popup);

        _order++;

        go.transform.SetParent(Root.transform);

        return popup;
    }

    public void PreloadToast()
    {
        if (_toastPopup != null && _toastPopup.gameObject != null)
            return;

        EnsureRootCanvasForToast();

        GameObject go = GameManager.Resources.Instantiate("UI/PopUp/ToastPopup");
        if (go == null)
        {
            Debug.LogWarning("[UI] ToastPopup prefab preload failed.");
            return;
        }

        go.transform.SetParent(Root.transform, false);
        go.transform.SetAsLastSibling();

        _toastPopup = Util.GetOrAddComponent<ToastPopup>(go);
        _toastPopup.Prepare();
        Debug.Log("[UI] ToastPopup prepared.");
    }

    public void ShowToast(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        EnsureRootCanvasForToast();
        PreloadToast();
        if (_toastPopup == null)
        {
            Debug.LogWarning($"[UI] ToastPopup is not available. Message={message}");
            return;
        }

        _toastPopup.transform.SetAsLastSibling();
        Debug.Log($"[UI] ToastPopup show. Message={message}");
        _toastPopup.Show(message);
    }


    public void ShowComboHit()
    {
        EnsureRootCanvasForToast();

        if (_comboCounter == null || _comboCounter.gameObject == null)
            _comboCounter = UI_ComboCounter.Create(Root.transform);

        _comboCounter.transform.SetAsLastSibling();
        _comboCounter.ShowHit();
    }
    private void EnsureRootCanvasForToast()
    {
        Canvas canvas = Util.GetOrAddComponent<Canvas>(Root);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        if (canvas.sortingOrder < 1000)
            canvas.sortingOrder = 1000;

        Util.GetOrAddComponent<GraphicRaycaster>(Root);

        CanvasScaler scaler = Util.GetOrAddComponent<CanvasScaler>(Root);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = ScreenMatchMode.MatchWidthOrHeight;
    }
    public T ShowSceneUI<T>(string name = null) where T : UI_Scene
    {
        if (string.IsNullOrEmpty(name) == true)
        {
            Debug.Log($"ShowSceneUI] param is null ");
            name = typeof(T).Name;
        }

        GameObject go = GameManager.Resources.Instantiate($"UI/Scene/{name}");

        T sceneUI = Util.GetOrAddComponent<T>(go);

        _scene = sceneUI;

        go.transform.SetParent(Root.transform);

        return sceneUI;
    }


    public void ClosePopupUI(UI_PopUp popup)
    {
        if (popup == null)
            return;

        if (_popupStack.Count <= 0)
        { 
            Debug.Log($"Popup UI is empty"); 
            return; 
        }

        if (_popupStack.Peek() != popup)
        {
            return;
        }
        ClosePopupUI();
    }


    private void ClosePopupUI()
    {
        if (_popupStack.Count <= 0)
        { Debug.Log($"Popup UI is empty"); return; }

        UI_PopUp popup = _popupStack.Pop();
        _order--;

        GameManager.Resources.Destroy(popup.gameObject);
        popup = null;

    }

    public void CloseAllPopupUI()
    {
        while (_popupStack.Count > 0)
        {
            ClosePopupUI();
        }
    }
}




