using Server.Protocol;

[System.Serializable]
public class CombatEventData
{
    public CombatEventType EventType;
    public int AttackerId;
    public int TargetId;
    public ActionType ActionType;
    public int Value;
}