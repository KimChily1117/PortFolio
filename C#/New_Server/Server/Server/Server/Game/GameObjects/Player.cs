using Server.Protocol;
using Server.Session;

namespace Server.Game.GameObjects
{
    public class Player : Creature
    {
        public ClientSession Session { get; set; }

        public uint LastAckInputSeq { get; set; }

        public bool HasPendingAction { get; set; }
        public ActionType PendingActionType { get; set; } = ActionType.ActionNone;
        public int ActionDirX { get; set; }
        public int ActionDirY { get; set; }
        public uint LastActionInputSeq { get; set; }

        public int SpawnProtectionEndTick { get; set; }
        public int LastJumpRequestTick { get; set; }    
        public int NextActionTick { get; set; }
        public bool IsAttacking { get; set; }

        public Player()
        {
            ObjectType = GameObjectType.Player;
            Speed = 10;
            MaxHp = 100;
            Hp = 100;
            MainState = ActorMainState.Idle;
        }

    }
}