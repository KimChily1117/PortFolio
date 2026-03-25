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

        private const int AttackRange = 50;
        private const int AttackDamage = 10;
        private const int AttackCooldownTick = 15;

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

        private void SpawnTestTargetIfNeeded()
        {
            const int testTargetId = 1000;

            if (_players.ContainsKey(testTargetId))
                return;

            Player dummy = new Player();
            dummy.Id = testTargetId;
            dummy.PosX = 180;
            dummy.PosY = 0;
            dummy.Speed = 0;
            dummy.Hp = 30;
            dummy.MaxHp = 30;
            dummy.MainState = ActorMainState.Idle;

            _players.Add(dummy.Id, dummy);
            dummy.MarkDirty();

            Console.WriteLine("[Room] TestTarget Spawned. id=" + dummy.Id +
                " pos=(" + dummy.PosX + "," + dummy.PosY + ")" +
                " hp=" + dummy.Hp);
        }

        private bool CanUseAction(Player player)
        {
            if (player == null)
                return false;

            if (player.IsDead)
                return false;

            if (ServerTick < player.NextActionTick)
                return false;

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
            if (attacker == null)
                return;

            attacker.IsAttacking = true;
            attacker.NextActionTick = ServerTick + AttackCooldownTick;

            // 1) 공격 시작 이벤트
            AddCombatEvent(
                CombatEventType.CombatEventAttack,
                attacker.Id,
                0,
                ActionType.ActionAttack,
                0);

            Console.WriteLine("[Attack] tick=" + ServerTick +
                " attacker=" + attacker.Id +
                " nextActionTick=" + attacker.NextActionTick);

            // 2) 타겟 탐색
            Player target = FindTargetInRange(attacker, AttackRange);
            if (target == null)
            {
                Console.WriteLine("[HitCheck] attacker=" + attacker.Id + " no target in range");
                return;
            }

            // 3) 데미지 적용
            int beforeHp = target.Hp;
            target.Hp -= AttackDamage;
            if (target.Hp < 0)
                target.Hp = 0;

            target.MarkDirty();

            Console.WriteLine("[Hit] tick=" + ServerTick +
                " attacker=" + attacker.Id +
                " target=" + target.Id +
                " damage=" + AttackDamage +
                " hp=" + beforeHp + "->" + target.Hp);

            // 4) 히트 이벤트
            AddCombatEvent(
                CombatEventType.CombatEventHit,
                attacker.Id,
                target.Id,
                ActionType.ActionAttack,
                AttackDamage);

            // 5) 죽음 이벤트
            if (target.IsDead)
            {
                target.MoveInputX = 0;
                target.MoveInputY = 0;
                target.MainState = ActorMainState.Idle;
                target.MarkDirty();

                AddCombatEvent(
                    CombatEventType.CombatEventDeath,
                    attacker.Id,
                    target.Id,
                    ActionType.ActionAttack,
                    0);

                Console.WriteLine("[Death] tick=" + ServerTick +
                    " target=" + target.Id);
            }
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
            {
                if (player.Session == null)
                    continue;

                player.Session.SendProto(packet);
            }
        }
    }
}