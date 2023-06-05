using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using Photon.Realtime;

public class RoomUI : MonoBehaviourPunCallbacks
{
    public Button btn_Leave;

    private void Start()
    {
        Debug.Log($"On Start Room Scene");

        if (btn_Leave) 
        {
            btn_Leave.onClick.RemoveAllListeners();
            btn_Leave.onClick.AddListener(LeaveRoom);
        }

        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.Instantiate("unitychan", Vector3.zero, Quaternion.identity);
        }
    }


    public void LeaveRoom()
    { 
        PhotonNetwork.LeaveRoom();
    }

}
