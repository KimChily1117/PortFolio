using Server.Protocol;
using Server.Session;
using Server.Protocol;

namespace Server.Game.GameObjects
{
    public sealed class Player : GameObject
    {
        public bool IsDirty { get; private set; } = true;

        public int MoveInputX { get; set; }
        public int MoveInputY { get; set; }

        public uint LastAckInputSeq { get; set; }

        public int Speed { get; set; } = 10;

        public bool HasPendingAction { get; set; }
        public ActionType PendingActionType { get; set; } = ActionType.ActionNone;
        public int ActionDirX { get; set; }
        public int ActionDirY { get; set; }
        public uint LastActionInputSeq { get; set; }
        public int NextActionTick { get; set; } = 0;
        public bool IsAttacking { get; set; } = false;
        public ActorMainState MainState { get; set; } = ActorMainState.Idle;
        public int MaxHp { get; set; } = 100;
        public int Hp { get; set; } = 100;
        public bool IsDead => Hp <= 0;
        public ClientSession Session { get; set; }
        public void MarkDirty()
        {
            IsDirty = true;
        }

        public void ClearDirty()
        {
            IsDirty = false;
        }
    
        
    }
}