using Server.Protocol;

namespace Server.Game.GameObjects
{
    public abstract class Creature : GameObject
    {
        public int MoveInputX { get; set; }
        public int MoveInputY { get; set; }

        public int Speed { get; set; } = 10;

        public ActorMainState MainState { get; set; } = ActorMainState.Idle;

        public int MaxHp { get; set; } = 100;
        public int Hp { get; set; } = 100;

        public bool IsJumping { get; set; }
        public int JumpEndTick { get; set; }

        public bool IsDead
        {
            get { return Hp <= 0; }
        }

        public virtual void OnDead()
        {
            MoveInputX = 0;
            MoveInputY = 0;
            IsJumping = false;
            MainState = ActorMainState.Dead;
            MarkDirty();
        }
    }
}