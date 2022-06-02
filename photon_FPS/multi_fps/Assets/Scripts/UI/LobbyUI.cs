using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using Photon.Realtime;


public class LobbyUI : MonoBehaviour
{
    [SerializeField]
    Dropdown Region_dropdown;

    public Text CurrentRegionText;


    [Tooltip("The Ui Panel to let the user enter name, connect and play")]
    [SerializeField]
    private GameObject controlPanel;

    [Tooltip("The UI Label to inform the user that the connection is in progress")]
    [SerializeField]
    private GameObject progressLabel;

    [SerializeField]
    private Text successText;

    [SerializeField]
    private GameObject createRoomPanel;

    [SerializeField]
    private GameObject JoinRoomPanel;


    [SerializeField]
    private GameObject disconnectButton;

    [SerializeField]
    private GameObject regionInfoPanel;

    [SerializeField]
    //private JoinRoomListUI joinRoomList;

    public Button btn_Connect;
    public Button btn_Disconnect;


    public InputField NameField;


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


        Region_dropdown.onValueChanged.RemoveAllListeners();
        SetRegionDropDownvalue((int)Define.RegionType.ASIA);
        Region_dropdown.onValueChanged.AddListener(SetRegionDropDownvalue);
                
        if (btn_Connect != null)
        {
            btn_Connect.onClick.RemoveAllListeners();
            btn_Connect.onClick.AddListener(() => GameManager.Network.OnClickConnect());
        }

        if (btn_Disconnect != null)
        {
            btn_Disconnect.onClick.RemoveAllListeners();
            btn_Connect.onClick.AddListener(() => GameManager.Network.OnClickDisconnect());

        }

        NameField.onValueChanged.RemoveAllListeners();
        NameField.onValueChanged.AddListener(SetPlayerName);


    }

    public void SetRegionDropDownvalue(int value)
    {
        Debug.LogError($"Select index? {value}");
        Region_dropdown.value = value;        
        SetCurrentRegionText(value);
    }
    private void SetCurrentRegionText(int value)
    {
        string temp_text = string.Empty;

        switch ((Define.RegionType)value)
        {
            case Define.RegionType.ASIA:
                temp_text = "�ƽþ�";
                break;
            case Define.RegionType.CHINESE:
                temp_text = "�߱�";
                break;
            case Define.RegionType.EUROPE:
                temp_text = "����";
                break;
            case Define.RegionType.JAPAN:
                temp_text = "�Ϻ�";
                break;
            case Define.RegionType.SOUTH_AFRICA:
                temp_text = "��������ī";
                break;
            case Define.RegionType.SOUTH_KOREA:
                temp_text = "���ѹα�";
                break;
            case Define.RegionType.USA_EAST:
                temp_text = "�̵���";
                break;
            case Define.RegionType.USA_WEST:
                temp_text = "�̼���";
                break;
        }
        CurrentRegionText.text = $"���缭�� : {temp_text}";

        GameManager.Network.SetRegion((Define.RegionType)value);
    }



    public void SetPlayerName(string value)
    {
        if(string.IsNullOrEmpty(value))
        {
            return;
        }

        PhotonNetwork.NickName = value;
    }
}
