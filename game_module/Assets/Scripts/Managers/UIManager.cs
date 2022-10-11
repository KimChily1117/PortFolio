using System.Collections;
using System.Collections.Generic;
using UnityEngine;



// UI의 sort order를 
public class UIManager
{
    public int _order = 10;
    Stack<UI_PopUp> _popupStack = new Stack<UI_PopUp>();

    UI_Scene _scene = null;

    public GameObject Root
    { 
      get 
      {
        GameObject root = GameObject.Find("@UI_Root");

        if(root == null)
        {
          root = new GameObject{ name = "@UI_Root"};
        }

        return root;
      } 
    }


    public void SetCanvas(GameObject go , bool sort = true)
    {
        Canvas canvas = Util.GetOrAddComponent<Canvas>(go);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        canvas.overrideSorting = true;
        if(sort) //팝업이랑 관련 있는 Sorting이 필요한 UI
        {
            canvas.sortingOrder = _order;
            _order++;
        }
        else
        {
            canvas.sortingOrder = 0;
        }

        go.transform.SetParent(Root.transform);
    }


    public T ShowPopupUI<T>(string name = null) where T : UI_PopUp
    {
        if(string.IsNullOrEmpty(name) == true)
        {
            Debug.Log($"Param is Null or Empty ");
            Debug.Log($"Type of Generic T {typeof(T).Name} ");
            name = typeof(T).Name;
        }        
        string path = $"UI/Popup/{name}";
        
        GameObject go = GameManager.Resources.Instantiate(path);

        T popup = Util.GetOrAddComponent<T>(go);

        _popupStack.Push(popup);
        
        _order++;

        go.transform.SetParent(Root.transform);

        return popup;
    }

    public T ShowSceneUI<T>(string name = null) where T : UI_Scene 
    {
        if(string.IsNullOrEmpty(name) == true) 
        {
            Debug.Log($"ShowSceneUI] param is null ");
            name = typeof(T).Name;
        }
        
        GameObject go = GameManager.Resources.Instantiate($"UI/Scene/{name}");
        
        T sceneUI = Util.GetOrAddComponent<T>(go);

        _scene = sceneUI;

        return null;
    }


    public void ClosePopupUI(UI_PopUp popup)
    {
         if(_popupStack.Count <= 0)
        { Debug.Log ($"Popup UI is empty"); return; }
         
         if(_popupStack.Peek() != popup)
         {
             return;
         }
         ClosePopupUI();
    }


    private void ClosePopupUI()
    {
        if(_popupStack.Count <= 0)
        { Debug.Log ($"Popup UI is empty"); return; }

        UI_PopUp popup = _popupStack.Pop();
        _order--;

        GameManager.Resources.Destroy(popup.gameObject);        
        popup = null;

    }
    
    public void CloseAllPopupUI()
    {
        while(_popupStack.Count > 0)
        {
           ClosePopupUI();
        }
    }
}
