using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public class NetworkManager : MonoBehaviourPunCallbacks
{
    private TypedLobby testLobby = new TypedLobby("testLobby", LobbyType.Default);


}
