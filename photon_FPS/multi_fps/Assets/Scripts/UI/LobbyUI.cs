using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

public class LobbyUI : MonoBehaviour
{
    [SerializeField]
    Dropdown Region_dropdown;


    private void Awake()
    {
        InitializedUI();
    }
    
    public void InitializedUI()
    {
        if (Region_dropdown == null) { return; } //추후 UI 자동화 코드 추가 예정

        List<string> list = Enum.GetNames(typeof(Define.RegionType)).ToList();

        Region_dropdown.ClearOptions();
        Region_dropdown.AddOptions(list);

    }


    // Update is called once per frame
    void Update()
    {

    }


}
