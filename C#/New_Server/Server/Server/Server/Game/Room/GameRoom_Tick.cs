using Server.Game.GameObjects;
using Server.Protocol;
using System;

namespace Server.Game.Room
{
    public partial class GameRoom
    {
        private void UpdatePlayers()
        {
            foreach (Player player in _players.Values)
            {
                if (player.IsDead)
                    continue;

                int newX = player.PosX + player.MoveInputX * player.Speed;
                int newY = player.PosY + player.MoveInputY * player.Speed;

                if (player.PosX != newX || player.PosY != newY)
                {
                    player.PosX = newX;
                    player.PosY = newY;
                    player.MarkDirty();
                }

                if (player.IsAttacking && ServerTick >= player.NextActionTick)
                {
                    player.IsAttacking = false;
                }
            }

            if (ServerTick % 30 == 0)
            {
                foreach (Player player in _players.Values)
                {
                    Console.WriteLine("[Tick] tick=" + ServerTick +
                        " player=" + player.Id +
                        " hp=" + player.Hp +
                        " input=(" + player.MoveInputX + "," + player.MoveInputY + ")" +
                        " pos=(" + player.PosX + "," + player.PosY + ")" +
                        " dirty=" + player.IsDirty);
                }
            }
        }



        private void BroadcastSnapshot()
        {
            foreach (Player receiver in _players.Values)
            {
                if (receiver.Session == null)
                    continue;

                S_RoomSnapshot snapshot = new S_RoomSnapshot();
                snapshot.ServerTick = ServerTick;
                snapshot.AckInputSeq = receiver.LastAckInputSeq;

                foreach (Player player in _players.Values)
                {
                    if (!player.IsDirty)
                        continue;

                    ActorSnapshot actor = new ActorSnapshot();
                    actor.ActorId = player.Id;
                    actor.MainState = player.MainState;
                    actor.Pos = new Vec2Int
                    {
                        X = player.PosX,
                        Y = player.PosY
                    };

                    snapshot.Actors.Add(actor);
                }

                receiver.Session.SendProto(snapshot);

                Console.WriteLine("[Snapshot] tick=" + ServerTick +
                    " receiver=" + receiver.Id +
                    " ack=" + receiver.LastAckInputSeq +
                    " actorCount=" + snapshot.Actors.Count);
            }

            foreach (Player player in _players.Values)
                player.ClearDirty();
        }
        private bool HasAnyDirtyPlayer()
        {
            foreach (Player player in _players.Values)
            {
                if (player.IsDirty)
                    return true;
            }

            return false;
        }
    }
}