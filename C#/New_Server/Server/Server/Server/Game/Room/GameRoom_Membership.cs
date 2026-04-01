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
            player.Room = this;

            player.Hp = player.MaxHp;            
            player.IsJumping = false;
            player.SpawnProtectionEndTick = ServerTick + SpawnProtectionTick;

            _players.Add(player.Id, player);

            Console.WriteLine("[Room] Player Enter. id=" + player.Id +
                " spawnProtectionEndTick=" + player.SpawnProtectionEndTick);

            // 테스트용 enemy spawn 로직 있으면 그대로 유지
            if (_enemies.Count == 0)
            {
                Enemy enemy = new Enemy();
                enemy.Id = 1000;
                enemy.PosX = 40;   // 테스트용으로 플레이어 근처
                enemy.PosY = 0;
                enemy.Room = this;

                _enemies.Add(enemy.Id, enemy);

                Console.WriteLine("[Room] TestEnemy Spawned. id=" + enemy.Id +
                    " pos=(" + enemy.PosX + "," + enemy.PosY + ")" +
                    " hp=" + enemy.Hp);
            }

            BroadcastSnapshot();
        }

        private void HandleLeave(LeaveRoomCommand command)
        {
            Player player;
            if (_players.TryGetValue(command.PlayerId, out player) == false)
                return;

            _players.Remove(command.PlayerId);
            player.Room = null;

            System.Console.WriteLine("[Room] Player Leave. id=" + command.PlayerId);


            foreach (GameObject obj in GetAllObjects())
                obj.MarkDirty();
        }
    }
}