using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UI_Buttons : UI_Base
{

   
    [SerializeField] Text _text;

   
    void Start()
    {
        Bind<Button>(typeof(Buttons)); // typeof로 형변환을 하여 C#에서 기본으로 제공하는 object 클래스를 상속받는 객체를 매개변수로 넘긴다.
        Bind<Text>(typeof(Texts)); // typeof로 형변환을 하여 C#에서 기본으로 제공하는 object 클래스를 상속받는 객체를 매개변수로 넘긴다.

        Get<Text>((int)Texts.PointText).text = "Getter Test";
    }


    


   
    // Start is called before the first frame update

    // Update is called once per frame
    void Update()
    {
        
    }
}
