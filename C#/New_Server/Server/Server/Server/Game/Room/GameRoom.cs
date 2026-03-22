using Google.Protobuf;
using Server.Game.GameObjects;
using Server.Protocol;
using System;
using System.Collections.Generic;

namespace Server.Game.Room
{
    public partial class GameRoom
    {
        public int RoomId { get; set; }
        public int ServerTick { get; private set; }

        private int _snapshotElapsedMs = 0;

        public const int TickMs = 33;              // 현재 루프 기준
        private const int SnapshotIntervalMs = 100; // 100ms 송신


        private readonly Dictionary<int, Player> _players = new Dictionary<int, Player>();
        private readonly Queue<RoomCommand> _pendingCommands = new Queue<RoomCommand>();
        private readonly List<RoomCombatEvent> _pendingCombatEvents = new List<RoomCombatEvent>();

        public void Enqueue(RoomCommand command)
        {
            lock (_pendingCommands)
            {
                _pendingCommands.Enqueue(command);
            }
        }

        public void Tick()
        {
            ServerTick++;
            _snapshotElapsedMs += TickMs;

            ConsumeCommands();
            UpdatePlayers();
            ResolveActions();

            if (_snapshotElapsedMs >= SnapshotIntervalMs)
            {
                if (HasAnyDirtyPlayer())
                {
                    BroadcastSnapshot();
                }
                _snapshotElapsedMs -= SnapshotIntervalMs;
            }
            BroadcastCombatEvents();
        }



        private bool CanUseAction(Player player)
        {
            // 쿨타임 체크
            if (ServerTick < player.NextActionTick)
                return false;

            // 이미 공격 중인지
            if (player.IsAttacking)
                return false;

            return true;
        }

        private void ResolveActions()
        {
            foreach (Player player in _players.Values)
            {
                if (!player.HasPendingAction)
                    continue;

                Console.WriteLine("[ResolveActions] player=" + player.Id +
                    " action=" + player.PendingActionType +
                    " canUse=" + CanUseAction(player));

                if (!CanUseAction(player))
                {
                    player.HasPendingAction = false;
                    continue;
                }

                switch (player.PendingActionType)
                {
                    case ActionType.ActionAttack:
                        ExecuteAttack(player);
                        break;
                }

                player.HasPendingAction = false;
            }
        }

        private void ExecuteAttack(Player attacker)
        {
            // 공격 상태 진입
            attacker.IsAttacking = true;

            // 쿨타임 설정 (예: 0.5초 = 15 tick)
            attacker.NextActionTick = ServerTick + 15;

            RoomCombatEvent ev = new RoomCombatEvent
            {
                EventType = CombatEventType.CombatEventAttack,
                AttackerId = attacker.Id,
                TargetId = 0,
                ActionType = ActionType.ActionAttack,
                Value = 0
            };

            _pendingCombatEvents.Add(ev);

            Console.WriteLine("[Attack] tick=" + ServerTick +
                " attacker=" + attacker.Id +
                " nextActionTick=" + attacker.NextActionTick);
        }


        private void BroadcastCombatEvents()
        {
            if (_pendingCombatEvents.Count == 0)
                return;

            S_CombatEvents packet = new S_CombatEvents();
            packet.ServerTick = ServerTick;

            foreach (RoomCombatEvent ev in _pendingCombatEvents)
            {
                CombatEvent protoEvent = new CombatEvent();
                protoEvent.EventType = ev.EventType;
                protoEvent.AttackerId = ev.AttackerId;
                protoEvent.TargetId = ev.TargetId;
                protoEvent.ActionType = ev.ActionType;
                protoEvent.Value = ev.Value;

                packet.Events.Add(protoEvent);
            }

            Broadcast(packet);

            Console.WriteLine("[CombatEvents] tick=" + ServerTick +
                " count=" + packet.Events.Count);

            _pendingCombatEvents.Clear();
        }

        private void ConsumeCommands()
        {
            while (true)
            {
                RoomCommand command = null;

                lock (_pendingCommands)
                {
                    if (_pendingCommands.Count == 0)
                        break;

                    command = _pendingCommands.Dequeue();
                }

                HandleCommand(command);
            }
        }

        private void HandleCommand(RoomCommand command)
        {
            if (command is EnterRoomCommand)
                HandleEnter((EnterRoomCommand)command);
            else if (command is LeaveRoomCommand)
                HandleLeave((LeaveRoomCommand)command);
            else if (command is MoveInputCommand)
                HandleMoveInput((MoveInputCommand)command);
            else if (command is ActionInputCommand)
                HandleActionInput((ActionInputCommand)command);
        }


        private void Broadcast(IMessage packet)
        {
            foreach (Player player in _players.Values)
                player.Session.SendProto(packet);
        }
    }
}