using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Define
{
    public enum CameraView
    {
        QuaterView
    }

    public enum MouseEvent
    {
        Press,
        Click
    }

    public enum KeyEvent
    {
        Down,
        Up,
        DoubleBtn
    }
    
    
    public enum UIEvent
    {
        Click,
        Drag
    }

    public enum Scenes
    {
      NONE,
      LOGIN,
      LOBBY,
      TOWN,
      DUNGEONSELECT,
      BAKAL
    }

    public enum SoundType
    {
        BGM,
        Effect,
        MaxCount
    }
    
    //public enum PlayerState
    //{
    //    NONE,
    //    IDLE,
    //    WALK,
    //    RUN,
    //    JUMP,
    //    ATKIDLE,
    //    ATK1,
    //    ATK2,
    //    ATK3,
    //}

    public enum InputType
    {
        Up,
        Down,
        Left,
        Right,
        ATK,
        Jump
    }

    public enum SkillType
    {
    }

}
