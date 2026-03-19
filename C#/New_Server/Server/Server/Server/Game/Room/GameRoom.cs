using Google.Protobuf;
using Microsoft.EntityFrameworkCore.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;

namespace Server.Game.Room
{
    public partial class GameRoom : JobSerializer
    {
        public int RoomId { get; set; }
        public int ServerTick { get; private set; }

        //private readonly Dictionary<int, Player> _players = new Dictionary<int, Player>();
        //private readonly Dictionary<int, Enemy> _enemies = new Dictionary<int, Enemy>();
        //private readonly Dictionary<int, Projectile> _projectiles = new Dictionary<int, Projectile>();

        //private readonly Queue<RoomCommand> _pendingCommands = new Queue<RoomCommand>();
        //private readonly List<IMessage> _eventBuffer = new List<IMessage>();

        //public void Enqueue(RoomCommand command)
        //{
        //    if (command == null)
        //        return;

        //    lock (_pendingCommands)
        //    {
        //        _pendingCommands.Enqueue(command);
        //    }
        //}

        public void Tick()
        {
            ServerTick++;

            ConsumeCommands();


            //UpdatePlayers();
            //UpdateEnemies();
            //UpdateProjectiles();
            //ResolveCombat();
            //BroadcastSnapshot();



            //BroadcastEvents();
        }

        private void ConsumeCommands()
        {
            while (true)
            {
                //RoomCommand command = null;

                //lock (_pendingCommands)
                //{
                //    if (_pendingCommands.Count == 0)
                //        break;

                //    command = _pendingCommands.Dequeue();
                //}

                //HandleCommand(command);
            }
        }

        //private void HandleCommand(RoomCommand command)
        //{
        //    switch (command)
        //    {
        //        case EnterRoomCommand enter:
        //            HandleEnter(enter);
        //            break;

        //        case LeaveRoomCommand leave:
        //            HandleLeave(leave);
        //            break;

        //        case MoveInputCommand move:
        //            HandleMoveInput(move);
        //            break;

        //        case ActionInputCommand action:
        //            HandleActionInput(action);
        //            break;
        //    }
        //}

        //private void BroadcastEvents()
        //{
        //    if (_eventBuffer.Count == 0)
        //        return;

        //    foreach (IMessage message in _eventBuffer)
        //        Broadcast(message);

        //    _eventBuffer.Clear();
        //}

        //public void Broadcast(IMessage message)
        //{
        //    foreach (Player player in _players.Values)
        //        player.Session.Send(message);
        //}

    }
}
