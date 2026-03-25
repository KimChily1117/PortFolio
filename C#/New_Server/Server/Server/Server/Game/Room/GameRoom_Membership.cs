using Server.Game.GameObjects;
using System;
using Server.Protocol;

namespace Server.Game.Room
{
    public partial class GameRoom
    {
        private void HandleEnter(EnterRoomCommand command)
        {
            Player player = command.Player;
            if (player == null)
                return;

            if (_players.ContainsKey(player.Id))
                return;

            player.Room = this; // 중요
            player.MoveInputX = 0;
            player.MoveInputY = 0;
            player.MainState = ActorMainState.Idle;

            _players.Add(player.Id, player);

            Console.WriteLine("[Room] Player Enter. id=" + player.Id);

            SpawnTestTargetIfNeeded();

            foreach (Player p in _players.Values)
                p.MarkDirty();
        }

        private void HandleLeave(LeaveRoomCommand command)
        {
            Player player;
            if (_players.TryGetValue(command.PlayerId, out player) == false)
                return;

            _players.Remove(command.PlayerId);
            player.Room = null;

            System.Console.WriteLine("[Room] Player Leave. id=" + command.PlayerId);


            foreach (Player p in _players.Values)
                p.MarkDirty();
        }
    }
}