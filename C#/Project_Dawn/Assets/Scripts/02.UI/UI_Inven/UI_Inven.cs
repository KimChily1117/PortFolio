using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class UI_Inven : UI_PopUp
{
   enum GameObjects
   {
      Inventory_Panel,
   }

   public override void Init()
   {
      base.Init();
      
      Bind<GameObject>(typeof(GameObjects));

      GameObject Grid = Get<GameObject>((int)GameObjects.Inventory_Panel);


      Debug.Log($"Grid Init ? : {Grid.name}");

      foreach (Transform Child in Grid.transform)
      {
          Destroy(Child.gameObject);
      }

      for (int i = 0; i < 25; i++)
      {
          GameObject item = GameManager.Resources.Instantiate("UI/UI_Item/UI_Inventory_Item",Grid.transform);
          
          UI_Inven_Item invenitem = Util.GetOrAddComponent<UI_Inven_Item>(item);

          invenitem.SetData($"샛별의 숨소리 {i + 1 }번");
      }

   }

    protected override void Start() 
    {
      Init();
    }


}
