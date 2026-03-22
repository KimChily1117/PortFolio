using Server.Protocol;

namespace Server.Game.Room
{
    public class RoomCombatEvent
    {
        public CombatEventType EventType { get; set; }
        public int AttackerId { get; set; }
        public int TargetId { get; set; }
        public ActionType ActionType { get; set; }
        public int Value { get; set; }
    }
}