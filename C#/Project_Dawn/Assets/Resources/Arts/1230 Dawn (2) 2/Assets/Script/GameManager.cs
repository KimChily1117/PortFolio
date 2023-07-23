using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public P_Responner responnerPlayer;
    public Responner responnerMonster;
    public CameraTracker objCamera;
    public Behimoth_Start_Camera_Postion Start_Pos_Camera;
    public Behimoth_Worp objPlayer;
    public Behimoth_StartPosition StartPos;
    //public Player Hud_Player;
    public Player_HpBar hpbar;
    public Enamy_HpBar E_Bar;

    static GameManager Instance;
    public static GameManager GetInstance() // 게임매니저 .5싱글톤 구현 
    
    { return Instance;}
    /// <summary>
    /// ////////////////////////////////////////////////////////////////////////////////////////
    /// </summary>

   
    void AssignPlayer()
    {

        Player P_player = GetPlayer();

        Player_HpBar P_bar = hpbar.GetComponent<Player_HpBar>();

        if (P_player.Death() == false)
        {
            hpbar.objPlayer = responnerPlayer.objPlayer;
        }      

    }


    void AssignMonster()
    {
        Player E_player = GetEnamy();
        Enamy_HpBar bar = E_Bar.GetComponent<Enamy_HpBar>();

        if (E_player != null && E_player.Death() == false)
        { 
        E_player.m_nHP_Slider = bar.m_nHP_Slider;
        }
    }



    Player GetPlayer()
    {
        if (responnerPlayer.objPlayer)
        {
            return responnerPlayer.objPlayer.GetComponent<Player>();
        }
        return null;
    }

    Player GetEnamy()
    {
        if (responnerMonster.objPlayer != null)
        {
            return responnerMonster.objPlayer.GetComponent<Player>();
        }
        return null;
    }

    void ProcCamera()
    {
        CameraTracker cameratracker = objCamera.GetComponent<CameraTracker>();
        cameratracker.objTarget = responnerPlayer.objPlayer;

    }

    void Gate_to_Dungeon() // 이 objPlayer가 저쪽 위치 정보(dynamic을 쓰는 오브젝트)를 쓴다 이말이야
    {
        Behimoth_Worp behimoth = objPlayer.GetComponent<Behimoth_Worp>();

        CameraTracker cameratracker = objCamera.GetComponent<CameraTracker>();

        Behimoth_StartPosition behimoth_Start = StartPos.GetComponent<Behimoth_StartPosition>();

        behimoth.StartingPoint = behimoth_Start.Pos;

        cameratracker.In_Gate = behimoth.In_Gate;
    }

    void Tracking_to_Dungeon()
    {
        CameraTracker cameratracker = objCamera.GetComponent<CameraTracker>();

        Behimoth_Start_Camera_Postion behimoth_Start_camera = Start_Pos_Camera.GetComponent<Behimoth_Start_Camera_Postion>();

        cameratracker.vTargetPos = behimoth_Start_camera.Pos;
    }

    private void Awake()
    {
       Instance = this;
       //AssignPlayer();

    }

    
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        Gate_to_Dungeon();
        ProcCamera();

        if (objPlayer.Enter_Dungeon == true)
        {
            objPlayer.In_Gate = false;
            objCamera.In_Gate = false;
            Tracking_to_Dungeon();
        }
        AssignPlayer();
        AssignMonster();

    }
}

