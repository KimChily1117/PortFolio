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
        if (Region_dropdown == null) { return; } //���� UI �ڵ�ȭ �ڵ� �߰� ����

        List<string> list = Enum.GetNames(typeof(Define.RegionType)).ToList();

        Region_dropdown.ClearOptions();
        Region_dropdown.AddOptions(list);

    }


    // Update is called once per frame
    void Update()
    {

    }


}
