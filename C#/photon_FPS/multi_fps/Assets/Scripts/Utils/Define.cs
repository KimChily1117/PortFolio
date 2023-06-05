
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Define
{
    public enum RegionType
    {
        ASIA = 0,
        CHINESE,
        EUROPE,
        JAPAN,
        SOUTH_AFRICA,
        SOUTH_KOREA,
        USA_EAST,
        USA_WEST,       
    }

    public enum AgoraState : byte
    {
        NONE = 0,                       // 아고라 기능 사용 안함
        VIDEOCALL,                      // 아고라 VideoCall 기능 활성화 상태
        LIVESTREAMING_HOST,             // 아고라 LIVESTREAMING_HOST 기능 활성화 상태
        LIVESTREAMING_CLIENT,           // 아고라 LIVESTREAMING_CLIENT 기능 활성화 상태
        SCREEN_SHARE,                   // 아고라 SCREEN_SHARE 기능 활성화 상태 
    }


    public enum MouseEvent
    {
        Press,
        Click
    }


    public enum UIEvent
    {
        Click,
        Drag
    }


}
