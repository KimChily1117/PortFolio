using Character;
using Google.Protobuf.Protocol;
using UnityEngine;

public enum TownMapState
{
    SERIAROOM,
    DUNGEONENTRANCE
}

public class TownScene : BaseScene
{
    private const float MyRoomSpawnX = 0f;
    private const float MyRoomSpawnY = 0f;

    public static TownScene Current { get; private set; }

    public TownMapState CurrentMapState { get; set; }

    public TriggerEvent _seriaTriggerEvent;
    public TriggerEvent _dungeonTriggerEvent;

    public Transform _dungeonEntranceSpawn;
    private UI_PartyEntry PartyEntry { get; set; }
    private bool _isDungeonMatchRequesting;
    private bool _myPlayerPlacedInMyRoom;
    private bool _pendingChannelReenter;
    private TownMapState _channelReenterState;

    public CameraController _cameraController;

    public bool ShouldShowRemotePlayers => CurrentMapState != TownMapState.SERIAROOM;

    protected override void Initialize()
    {
        Debug.Log($"IngameScene ] InGameScene!!! ");

        Current = this;
        GameManager.SCENE.CurrentScene = Define.Scenes.TOWN;

        GameManager.ObjectManager.Clear();

        CurrentMapState = TownMapState.SERIAROOM;
        _myPlayerPlacedInMyRoom = false;

        GameManager.Network.ConnectToGame();

        GameManager.Sound.BGMStop();
        GameManager.Sound.Play("Sounds/seria_gate", Define.SoundType.BGM);
        GameManager.Sound.SetAudioVolume(0.55f);

        if (GameManager.PlayerPlatformType == Enums.PlatformType.MOBILE)
        {
            GameManager.UI.ShowSceneUI<UI_MobileController>("Dynamic_Joystick");
        }

        if (_seriaTriggerEvent == null)
        {
            _seriaTriggerEvent = GameObject.Find("SeriaPortal")?.GetComponent<TriggerEvent>();
        }

        if (_dungeonTriggerEvent == null)
        {
            _dungeonTriggerEvent = GameObject.Find("BakalPortal")?.GetComponent<TriggerEvent>();
        }

        if (_seriaTriggerEvent != null)
        {
            _seriaTriggerEvent.ClearTriggerEvent();
            _seriaTriggerEvent.AddTriggerEnterEvent(SeriaTriggerEnterEvent);
        }
        else
        {
            Debug.LogWarning("[TOWN_FLOW] SeriaPortal trigger not found. MyRoom to Lobby transition is disabled.");
        }

        if (_dungeonTriggerEvent != null)
        {
            _dungeonTriggerEvent.ClearTriggerEvent();
            _dungeonTriggerEvent.AddTriggerEnterEvent(DungeonTriggerEnterEvent);
        }
        else
        {
            Debug.LogWarning("[TOWN_FLOW] BakalPortal trigger not found. Dungeon matching trigger is disabled.");
        }

        _cameraController.SetCameraLimit(CurrentMapState);
        GameManager.UI.ShowSceneUI<UI_HUD>("HUD");
    }

    private void OnDestroy()
    {
        if (Current == this)
            Current = null;
    }

    public void RegisterSpawnedCharacter(BaseCharacter character, bool isMyPlayer)
    {
        if (character == null)
            return;

        if (isMyPlayer)
        {
            MyPlayer myPlayer = character as MyPlayer;
            if (_pendingChannelReenter)
                CompleteChannelReenter(myPlayer);
            else
                PlaceMyPlayerInMyRoom(myPlayer);
            return;
        }

        ApplyRemoteVisibility(character);
    }

    public void BeginChannelReenter()
    {
        _pendingChannelReenter = true;
        _channelReenterState = CurrentMapState;
        _isDungeonMatchRequesting = false;

        if (_dungeonTriggerEvent != null)
        {
            _dungeonTriggerEvent.ClearTriggerEvent();
            _dungeonTriggerEvent.AddTriggerEnterEvent(DungeonTriggerEnterEvent);
        }

        Debug.Log($"[TOWN_FLOW] Channel re-enter begin. PreserveState={_channelReenterState}");
    }

    private void CompleteChannelReenter(MyPlayer myPlayer)
    {
        if (myPlayer == null)
            return;

        TownMapState preservedState = _channelReenterState;
        _pendingChannelReenter = false;
        CurrentMapState = preservedState;
        _myPlayerPlacedInMyRoom = CurrentMapState == TownMapState.SERIAROOM;

        _cameraController.SetCameraLimit(CurrentMapState);
        GameManager.Sound.BGMStop();

        if (CurrentMapState == TownMapState.SERIAROOM)
        {
            GameManager.Sound.Play("Sounds/seria_gate", Define.SoundType.BGM);
            GameManager.Sound.SetAudioVolume(0.55f);
            Vector3 spawn = ResolveMyRoomSpawn();
            myPlayer.TeleportAndSync(spawn, MoveDir.Right);
        }
        else
        {
            GameManager.Sound.Play("Sounds/bakal_ready", Define.SoundType.BGM);
            GameManager.Sound.SetAudioVolume(0.55f);
            MoveDir facing = myPlayer._lastHorizontalDir == MoveDir.Left ? MoveDir.Left : MoveDir.Right;
            myPlayer.TeleportAndSync(myPlayer.transform.position, facing);
        }

        RefreshRemotePlayerVisibility();
        Debug.Log($"[TOWN_FLOW] Channel re-enter complete. State={CurrentMapState}, Player={myPlayer.ObjInfo?.Name}, ObjectId={myPlayer.Id}, Pos=({myPlayer.transform.position.x:0.00},{myPlayer.transform.position.y:0.00})");
    }
    public void RefreshRemotePlayerVisibility()
    {
        GameManager.ObjectManager.ForEachCharacter(character =>
        {
            if (character == null || character == GameManager.ObjectManager.MyPlayer)
                return;

            ApplyRemoteVisibility(character);
        });
    }

    private void ApplyRemoteVisibility(BaseCharacter character)
    {
        if (character == null)
            return;

        bool shouldShow = ShouldShowRemotePlayers;
        if (character.gameObject.activeSelf != shouldShow)
            character.gameObject.SetActive(shouldShow);

        Debug.Log($"[TOWN_FLOW] Remote visibility. Name={character.ObjInfo?.Name}, ObjectId={character.Id}, Visible={shouldShow}, State={CurrentMapState}");
    }

    private void PlaceMyPlayerInMyRoom(MyPlayer myPlayer)
    {
        if (myPlayer == null || _myPlayerPlacedInMyRoom)
            return;

        _myPlayerPlacedInMyRoom = true;
        CurrentMapState = TownMapState.SERIAROOM;
        _cameraController.SetCameraLimit(CurrentMapState);

        Vector3 spawn = ResolveMyRoomSpawn();
        myPlayer.TeleportAndSync(spawn, MoveDir.Right);
        RefreshRemotePlayerVisibility();

        Debug.Log($"[TOWN_FLOW] MyPlayer placed in MyRoom. Player={myPlayer.ObjInfo?.Name}, ObjectId={myPlayer.Id}, Pos=({spawn.x:0.00},{spawn.y:0.00})");
    }

    private Vector3 ResolveMyRoomSpawn()
    {
        GameObject spawn = GameObject.Find("MyRoomSpawn");
        if (spawn != null)
            return spawn.transform.position;

        return new Vector3(MyRoomSpawnX, MyRoomSpawnY, 0f);
    }

    private Vector3 ResolveLobbySpawn()
    {
        if (_dungeonEntranceSpawn != null)
            return _dungeonEntranceSpawn.position;

        GameObject spawn = GameObject.Find("LobbySpawn");
        if (spawn != null)
            return spawn.transform.position;

        return new Vector3(25.79f, 0f, 0f);
    }

    private void SeriaTriggerEnterEvent(Collider2D collider)
    {
        Debug.Log($"Seria Trigger Enter!! Collider={collider?.name}, Tag={collider?.tag}");

        if (collider == null || collider.CompareTag("Player") == false)
            return;

        MyPlayer myPlayer = collider.GetComponentInParent<MyPlayer>();
        if (myPlayer == null || GameManager.ObjectManager.MyPlayer != myPlayer)
        {
            Debug.Log($"[TRIGGER][SERIA_SKIP] Reason=NotLocalMyPlayer, Collider={collider.name}");
            return;
        }

        EnterLobbyFromMyRoom(myPlayer);
    }

    private void EnterLobbyFromMyRoom(MyPlayer myPlayer)
    {
        if (myPlayer == null)
            return;

        CurrentMapState = TownMapState.DUNGEONENTRANCE;
        _cameraController.SetCameraLimit(CurrentMapState);
        GameManager.Sound.BGMStop();
        GameManager.Sound.Play("Sounds/bakal_ready", Define.SoundType.BGM);

        Vector3 lobbySpawn = ResolveLobbySpawn();
        myPlayer.TeleportAndSync(lobbySpawn, MoveDir.Right);
        RefreshRemotePlayerVisibility();

        Debug.Log($"[TOWN_FLOW] MyRoom -> Lobby. Player={myPlayer.ObjInfo?.Name}, ObjectId={myPlayer.Id}, Pos=({lobbySpawn.x:0.00},{lobbySpawn.y:0.00})");
    }

    public void ResetDungeonTriggerEvent()
    {
        if (_dungeonTriggerEvent == null)
            _dungeonTriggerEvent = GameObject.Find("BakalPortal")?.GetComponent<TriggerEvent>();

        _isDungeonMatchRequesting = false;
        if (_dungeonTriggerEvent == null)
            return;

        _dungeonTriggerEvent.ClearTriggerEvent();
        _dungeonTriggerEvent.AddTriggerEnterEvent(DungeonTriggerEnterEvent);
    }

    private void DungeonTriggerEnterEvent(Collider2D collider)
    {
        Debug.Log($"Dungeon Trigger Enter!! Collider={collider?.name}, Tag={collider?.tag}");

        if (_isDungeonMatchRequesting)
            return;

        if (collider == null || collider.CompareTag("Player") == false)
            return;

        MyPlayer myPlayer = collider.GetComponentInParent<MyPlayer>();
        if (myPlayer == null || GameManager.ObjectManager.MyPlayer != myPlayer)
        {
            Debug.Log($"[TRIGGER][DUNGEON_SKIP] Reason=NotLocalMyPlayer, Collider={collider.name}");
            return;
        }

        if (CurrentMapState != TownMapState.DUNGEONENTRANCE)
        {
            Debug.Log($"[TRIGGER][DUNGEON_SKIP] Reason=NotInLobby, State={CurrentMapState}");
            return;
        }

        if (myPlayer.ObjInfo == null)
        {
            Debug.LogWarning("[TRIGGER][DUNGEON_SKIP] Reason=MissingPlayerInfo");
            return;
        }

        _isDungeonMatchRequesting = true;
        if (_dungeonTriggerEvent != null)
            _dungeonTriggerEvent.ClearTriggerEvent();

        C_CreateRoom c_room = new C_CreateRoom();
        c_room.Playerinfo = myPlayer.ObjInfo;
        GameManager.Network.Send(c_room);
    }
}



