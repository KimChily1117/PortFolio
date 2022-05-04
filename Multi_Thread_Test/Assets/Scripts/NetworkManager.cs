using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public class NetworkManager : MonoBehaviourPunCallbacks
{
    public override void OnConnectedToMaster()
    {
        base.OnConnectedToMaster();
    }
    public override void OnDisconnected(DisconnectCause cause) => print("연결끊킴");    
}