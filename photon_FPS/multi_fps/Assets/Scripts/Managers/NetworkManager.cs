using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public class NetworkManager : MonoBehaviourPunCallbacks
{
    private TypedLobby testLobby = new TypedLobby("testLobby", LobbyType.Default);

    public Dictionary<string, RoomInfo> cachedroomData = new Dictionary<string, RoomInfo>();
    // Dictionary�� key���� ���̸��� value���� �������� �־ �������ش�.

    string gameVersion = "1";


    public GameObject playerpref;

    #region ButtonActionMethod

    public void SetRegion(Define.RegionType type)
    {
        PhotonNetwork.PhotonServerSettings.AppSettings.FixedRegion = Utils.GetRegionToken(type);
    }

    public void OnClickConnect()
    {
        Debug.Log($"OnClick Connect button");
        if (PhotonNetwork.IsConnected)
        {
            // #Critical we need at this point to attempt joining a Random Room. If it fails, we'll get notified in OnJoinRandomFailed() and we'll create one.
            PhotonNetwork.JoinRandomRoom();
        }
        else
        {
            // #Critical, we must first and foremost connect to Photon Online Server.
            PhotonNetwork.GameVersion = "1";
            // ConnectToMaster, ConnectToRegion ��� ����
            PhotonNetwork.ConnectUsingSettings();
        }
    }

    public void OnClickDisconnect()
    {
        Debug.Log($"OnClick Disconnect button");

        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Disconnect();
        }
    }


    #endregion ButtonActionMethod


    #region photonCallbackMethod

    public override void OnConnectedToMaster()
    {
        Debug.Log("PUN Basics Tutorial/Launcher: OnConnectedToMaster() was called by PUN");

        // we don't want to do anything if we are not attempting to join a room.
        // this case where isConnecting is false is typically when you lost or quit the game, when this level is loaded, OnConnectedToMaster will be called, in that case
        // we don't want to do anything.
        if (PhotonNetwork.IsConnected)
        {           
            PhotonNetwork.JoinLobby(testLobby);
            //StartCoroutine(Co_CheckRoomList());
            // #Critical: The first we try to do is to join a potential existing room. If there is, good, else, we'll be called back with OnJoinRandomFailed()
            //PhotonNetwork.JoinRandomRoom();
        }
        else
        {
            Debug.LogError("Photon Disconnect");
        }
    }

    //public void InitRoom()
    //{
    //    cachedRoomList.Clear();

    //    cachedRoomList.Add("�����÷����������", new RoomInfo("�����÷����������", 69));
    //    cachedRoomList.Add("�̵��񽺻������", new RoomInfo("�̵��񽺻������", 34));
    //    cachedRoomList.Add("����Ʈ���������������", new RoomInfo("����Ʈ���������������", 37));
    //}

    public override void OnJoinedLobby()
    {
        Debug.Log("OnJoinedLobby");  
        
        if(PhotonNetwork.IsConnected)
        {
            PhotonNetwork.JoinOrCreateRoom("exam",
                new RoomOptions() { MaxPlayers = 4 }, testLobby);
        }
    }

    //private void UpdateCachedRoomList(List<RoomInfo> roomList)
    //{
    //    for (int i = 0; i < roomList.Count; i++)
    //    {
    //        RoomInfo info = roomList[i];
    //        if (info.RemovedFromList)
    //        {
    //            cachedRoomList.Remove(info.Name);
    //        }
    //        else
    //        {
    //            cachedRoomList[info.Name] = info;
    //        }
    //    }

    //    LauncherUI.UpdateRoomList(cachedRoomList);
    //}

   
    public override void OnLeftLobby()
    {
        Debug.Log("OnLeftLobby");

        //InitRoom();
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.Log("OnDisconnected");

        //InitRoom();

        //LauncherUI.OnDisconnectUI();

        Debug.LogWarningFormat("PUN Basics Tutorial/Launcher: OnDisconnected() was called by PUN with reason {0}", cause);
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        Debug.Log("PUN Basics Tutorial/Launcher:OnJoinRandomFailed() was called by PUN. No random room available, so we create one.\nCalling: PhotonNetwork.CreateRoom");

        // ���� �� ������ ������ ��� �� ����
        //PhotonNetwork.CreateRoom(null, new RoomOptions() { MaxPlayers = maxPlayersPerRoom });
    }

    public override void OnJoinedRoom()
    {
        Debug.Log("PUN Basics Tutorial/Launcher: OnJoinedRoom() called by PUN. Now this client is in a room.");

        // #Critical: We only load if we are the first player, else we rely on `PhotonNetwork.AutomaticallySyncScene` to sync our instance scene.
        if (PhotonNetwork.CurrentRoom.PlayerCount == 1)
        {
            Debug.Log($"We load the 'Room' playerCount : {PhotonNetwork.CurrentRoom.PlayerCount} ");

            // #Critical
            // Load the Room Level.
            PhotonNetwork.LoadLevel("Room");
        }

        else
        {
            Debug.Log($"We load the 'Room' playerCount : {PhotonNetwork.CurrentRoom.PlayerCount} ");

            PhotonNetwork.LoadLevel("Room");
        }
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.Log($"Player Info : {newPlayer}");

       
    }


    #endregion

    #region UnityMethod
    private void Start()
    {
        
    }

    #endregion
}
