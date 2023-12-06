using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UI_Inven_Item : UI_Base
{
  enum GameObjects
  {
      ItemIcon,
      ItemName
  }

  private string name;


  public override void Init()
  {
    Bind<GameObject>(typeof(GameObjects));

    Get<GameObject>((int)GameObjects.ItemName).GetComponent<Text>().text = name;

    Get<GameObject>((int)GameObjects.ItemIcon).BindEvent(OnClickIconEvent);
  }

  public void SetData(string _name)
  {
    Debug.Log($"ksy");
    name = _name;
  }

  private void Start() {
    
    Init();
  }

  private void OnClickIconEvent(PointerEventData data)
  {
    Debug.Log($"아이템을 클릭하셨습니다. 이름 : {name}");  
  }
  
}
