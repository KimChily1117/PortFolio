using Server.Game.GameObjects;

namespace Server.Game.Room
{
    public partial class GameRoom
    {
        private void HandleEnter(EnterRoomCommand command)
        {
            Player player = command.Player;
            if (player == null)
                return;

            _players[player.Id] = player;
            player.Room = this;

            System.Console.WriteLine("[Room] Player Enter. id=" + player.Id);

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