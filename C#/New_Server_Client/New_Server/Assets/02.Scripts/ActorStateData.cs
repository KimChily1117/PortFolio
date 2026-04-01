using Server.Protocol;

[System.Serializable]
public class ActorStateData
{
    public int ActorId;
    public ActorType ActorType;
    public ActorMainState MainState;
    public int PosX;
    public int PosY;
    public int Hp;
    public int MaxHp;
    public bool IsDead;
    public bool IsJumping;
}


[System.Serializable]
public class PatternZoneData
{
    public int ZoneId;
    public int PosX;
    public int PosY;
    public int Radius;
}