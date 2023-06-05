
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
        NONE = 0,                       // �ư�� ��� ��� ����
        VIDEOCALL,                      // �ư�� VideoCall ��� Ȱ��ȭ ����
        LIVESTREAMING_HOST,             // �ư�� LIVESTREAMING_HOST ��� Ȱ��ȭ ����
        LIVESTREAMING_CLIENT,           // �ư�� LIVESTREAMING_CLIENT ��� Ȱ��ȭ ����
        SCREEN_SHARE,                   // �ư�� SCREEN_SHARE ��� Ȱ��ȭ ���� 
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
