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
        Bind<Button>(typeof(Buttons)); // typeof�� ����ȯ�� �Ͽ� C#���� �⺻���� �����ϴ� object Ŭ������ ��ӹ޴� ��ü�� �Ű������� �ѱ��.
        Bind<Text>(typeof(Texts)); // typeof�� ����ȯ�� �Ͽ� C#���� �⺻���� �����ϴ� object Ŭ������ ��ӹ޴� ��ü�� �Ű������� �ѱ��.

        Get<Text>((int)Texts.PointText).text = "Getter Test";
    }


    


   
    // Start is called before the first frame update

    // Update is called once per frame
    void Update()
    {
        
    }
}
