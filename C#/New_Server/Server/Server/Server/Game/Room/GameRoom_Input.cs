using Server.Game.GameObjects;
using Server.Protocol;
using System;

namespace Server.Game.Room
{
    public partial class GameRoom
    {
        private void HandleMoveInput(MoveInputCommand command)
        {
            Player player = command.Player;
            if (player == null)
                return;

            bool changed =
                player.MoveInputX != command.MoveX ||
                player.MoveInputY != command.MoveY;

            player.MoveInputX = command.MoveX;
            player.MoveInputY = command.MoveY;
            player.LastAckInputSeq = command.InputSeq;

            ActorMainState newState =
                (command.MoveX == 0 && command.MoveY == 0)
                ? ActorMainState.Idle
                : ActorMainState.Move;

            if (player.MainState != newState)
            {
                player.MainState = newState;
                changed = true;
            }

            if (changed)
                player.MarkDirty();

            Console.WriteLine("[MoveInput] player=" + player.Id +
                " seq=" + command.InputSeq +
                " move=(" + command.MoveX + "," + command.MoveY + ")");
        }

        private void HandleActionInput(ActionInputCommand command)
        {
            Player player = command.Player;
            if (player == null)
                return;

            player.HasPendingAction = true;
            player.PendingActionType = command.ActionType;
            player.ActionDirX = command.DirX;
            player.ActionDirY = command.DirY;
            player.LastActionInputSeq = command.InputSeq;

            Console.WriteLine("[ActionInput] player=" + player.Id +
                " seq=" + command.InputSeq +
                " action=" + command.ActionType +
                " dir=(" + command.DirX + "," + command.DirY + ")");
        }
    }
}