using Google.Protobuf.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace DummyClient
{
    public sealed class DummyClient
    {
        private const float TownLobbyFallbackMinX = 18.0f;
        private const float TownLobbyFallbackMaxX = 33.0f;
        private const float TownLobbyFallbackMinY = -4.0f;
        private const float TownLobbyFallbackMaxY = 0.0f;

        private readonly object _lock = new object();
        private readonly TaskCompletionSource<bool> _completion = new TaskCompletionSource<bool>();
        private readonly Dictionary<int, ObjectInfo> _enemies = new Dictionary<int, ObjectInfo>();
        private readonly Dictionary<int, int> _collisionCountByTarget = new Dictionary<int, int>();
        private readonly HashSet<int> _deadEnemies = new HashSet<int>();
        private static readonly object RoomTrackLock = new object();
        private static readonly Dictionary<int, int> RoomIdByObjectId = new Dictionary<int, int>();
        private static readonly Dictionary<int, string> NameByObjectId = new Dictionary<int, string>();
        private bool _holdStarted;
        private bool _gameplayStarted;
        private bool _enemyMissingLogged;
        private bool _stopGameplayRequested;
        private DateTime _enteredDungeonAtUtc;
        private float _originX;
        private float _originY;
        private float _currentX;
        private float _currentY;
        private int _patrolDirection = 1;
        private int _patrolXDirection = 1;
        private int _patrolYDirection = 1;
        private int _patrolBoxSegment;
        private readonly string _patrolMode;
        private int _strafeDirection = 1;
        private bool _followArrivalIdleSent;
        private bool _lastInRange;
        private int _lastFollowTargetObjectId;
        private PlayerState _lastMoveState = PlayerState.Idle;
        private DateTime _nextStrafeSwitchAtUtc;
        private int _nextSkillIndex;

        public DummyClient(DummyClientOptions options, int index, string name)
        {
            Options = options;
            Index = index;
            Name = name;
            State = DummyClientState.Created;
            Session = new DummyClientSession(this);
            _patrolDirection = index % 2 == 0 ? 1 : -1;
            _patrolXDirection = _patrolDirection;
            _patrolYDirection = index % 2 == 0 ? 1 : -1;
            _patrolBoxSegment = index % 4;
            _patrolMode = ResolvePatrolMode(options, index);
            _strafeDirection = _patrolDirection;
        }

        public DummyClientOptions Options { get; }
        public int Index { get; }
        public string Name { get; }
        public DummyClientSession Session { get; }
        public DummyClientState State { get; private set; }
        public ObjectInfo PlayerInfo { get; set; }
        public int TargetRoomId { get; set; }
        public int TransferId { get; set; }
        public RoomType TargetRoomType { get; set; }
        public SceneType SceneType { get; set; }
        public HashSet<string> DummyMembers { get; } = new HashSet<string>();
        public HashSet<string> ExternalMembers { get; } = new HashSet<string>();

        public bool HasConnected { get; private set; }
        public bool HasLoggedIn { get; private set; }
        public bool HasPlayerReady { get; private set; }
        public bool HasEnteredTown { get; private set; }
        public bool HasMatchRequested { get; private set; }
        public bool HasWaitingForExternal { get; private set; }
        public bool HasPartyMatched { get; private set; }
        public bool HasSceneMoveReceived { get; private set; }
        public bool HasSceneReadySent { get; private set; }
        public bool HasEnteredDungeon { get; private set; }
        public bool HasGameplayStarted { get; private set; }
        public bool HasGameplayCompleted { get; private set; }
        public int MoveSentCount { get; private set; }
        public int SkillSentCount { get; private set; }
        public int LastSkillId { get; private set; }
        public float LastPosX { get; private set; }
        public float LastPosY { get; private set; }
        public bool HasEnemyTracked { get; private set; }
        public bool HasFollowReached { get; private set; }
        public int EnemyCount => GetEnemyCount();
        public int FollowShouldMoveCount { get; private set; }
        public int FollowIdleCount { get; private set; }
        public int FollowArrivedCount { get; private set; }
        public int IdleOnArrivalSentCount { get; private set; }
        public int StrafeMoveSentCount { get; private set; }
        public int DungeonBoundsClampCount { get; private set; }
        public PlayerState LastMoveState => _lastMoveState;
        public bool LastInRange => _lastInRange;
        public int TargetEnemyObjectId { get; private set; }
        public float LastTargetX { get; private set; }
        public float LastTargetY { get; private set; }
        public float LastTargetDistance { get; private set; }
        public string PatrolModeName => _patrolMode;
        public int CollisionSentCount { get; private set; }
        public int CollisionSkippedOutOfRangeCount { get; private set; }
        public int CollisionSkippedNoTargetCount { get; private set; }
        public int CollisionSkippedDeadTargetCount { get; private set; }
        public int CollisionSkippedLimitCount { get; private set; }
        public int LastCollisionTargetId { get; private set; }
        public int AddItemReceivedCount { get; private set; }
        public int LastRewardTemplateId { get; private set; }
        public int LastRewardCount { get; private set; }
        public bool RewardObserved => AddItemReceivedCount > 0;
        public int ReceivedMoveCount { get; private set; }
        public int CrossRoomMoveSuspectedCount { get; private set; }
        public DateTime ConnectStartedAtUtc { get; private set; }
        public DateTime ConnectedAtUtc { get; private set; }
        public DateTime LoggedInAtUtc { get; private set; }
        public DateTime PlayerReadyAtUtc { get; private set; }
        public DateTime EnteredTownAtUtc { get; private set; }
        public DateTime GameplayStartedAtUtc { get; private set; }
        public DateTime GameplayCompletedAtUtc { get; private set; }
        public DateTime TownLoadStartedAtUtc { get; private set; }
        public DateTime TownLoadCompletedAtUtc { get; private set; }
        public DateTime ScenarioCompletedAtUtc { get; private set; }

        public Task Completion => _completion.Task;

        public void Connect(IPEndPoint endPoint)
        {
            try
            {
                ConnectStartedAtUtc = DateTime.UtcNow;
                Socket socket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                socket.Connect(endPoint);
                Session.Start(socket);
                Session.OnConnected(endPoint);
            }
            catch (Exception ex)
            {
                Fail($"Connect failed: {ex.Message}");
            }
        }

        public void SetState(DummyClientState state)
        {
            lock (_lock)
            {
                State = state;
            }
        }

        public void MarkConnected()
        {
            HasConnected = true;
            ConnectedAtUtc = DateTime.UtcNow;
            SetState(DummyClientState.Connected);
        }

        public void MarkLoggedIn(bool loginOk)
        {
            HasLoggedIn = loginOk;
            LoggedInAtUtc = DateTime.UtcNow;
            SetState(DummyClientState.LoggedIn);
        }

        public void MarkPlayerReady()
        {
            HasPlayerReady = true;
            PlayerReadyAtUtc = DateTime.UtcNow;
            SetState(DummyClientState.PlayerReady);
        }

        public void MarkEnteredTown()
        {
            HasEnteredTown = true;
            EnteredTownAtUtc = DateTime.UtcNow;
            SetState(DummyClientState.EnteredTown);
        }

        public void MarkWaitingForExternal()
        {
            HasWaitingForExternal = true;
            SetState(DummyClientState.WaitingForExternal);
        }

        public void MarkPartyMatched()
        {
            HasPartyMatched = true;
            SetState(DummyClientState.PartyMatched);
        }

        public void MarkSceneMoveReceived()
        {
            HasSceneMoveReceived = true;
            SetState(DummyClientState.SceneMoveReceived);
        }

        public void MarkEnteredDungeon()
        {
            HasEnteredDungeon = true;
            _enteredDungeonAtUtc = DateTime.UtcNow;
            SetState(DummyClientState.EnteredDungeon);
            RegisterKnownObject(PlayerInfo?.ObjectId ?? 0, Name, TargetRoomId);
            Log($"EnteredDungeon marked. RoomId={TargetRoomId}, TransferId={TransferId}, GameplayEnabled={Options.HasGameplayEnabled}, GameplayStartDelayMs={Options.GameplayStartDelayMs}");
            CaptureGameplayOrigin();
            BeginGameplayIfReady();
        }

        public void SendCreateRoom()
        {
            C_CreateRoom packet = new C_CreateRoom();
            if (PlayerInfo != null)
                packet.Playerinfo = PlayerInfo;

            Session.Send(packet);
            HasMatchRequested = true;
            SetState(DummyClientState.MatchRequested);
            Log("C_CreateRoom sent.");
        }

        public void SendSceneReady()
        {
            if (HasSceneReadySent)
                return;

            C_SceneReady packet = new C_SceneReady
            {
                TargetRoomId = TargetRoomId,
                TransferId = TransferId,
                TargetRoomType = TargetRoomType,
                SceneType = SceneType
            };

            Session.Send(packet);
            HasSceneReadySent = true;
            SetState(DummyClientState.SceneReadySent);
            Log($"C_SceneReady sent. RoomId={TargetRoomId}, TransferId={TransferId}, RoomType={TargetRoomType}, SceneType={SceneType}");
        }

        public void TrackSpawnObjects(IEnumerable<ObjectInfo> objects)
        {
            if (objects == null)
                return;

            int tracked = 0;
            int inspected = 0;
            lock (_lock)
            {
                foreach (ObjectInfo obj in objects)
                {
                    inspected++;
                    if (obj == null)
                    {
                        LogVerbose("[SPAWN] Object=null");
                        continue;
                    }

                    GameObjectType detectedType = GetObjectTypeById(obj.ObjectId);
                    PositionInfo pos = obj.PosInfo;
                    RegisterKnownObject(obj.ObjectId, obj.Name, TargetRoomId);
                    LogVerbose($"[SPAWN] ObjId={obj.ObjectId}, Name={obj.Name}, DetectedType={detectedType}, Pos=({pos?.PosX ?? 0f:0.00},{pos?.PosY ?? 0f:0.00}), HasPos={pos != null}, RoomId={TargetRoomId}");

                    if (pos == null)
                        continue;

                    if (detectedType != GameObjectType.Enemy)
                        continue;

                    if (_deadEnemies.Contains(obj.ObjectId))
                    {
                        LogVerbose($"[SPAWN] Enemy ignored because it is already marked dead. ObjId={obj.ObjectId}");
                        continue;
                    }

                    _enemies[obj.ObjectId] = obj;
                    tracked++;
                    _enemyMissingLogged = false;
                    LogVerbose($"[SPAWN] Enemy stored. ObjId={obj.ObjectId}, Pos=({pos.PosX:0.00},{pos.PosY:0.00})");
                }

                if (_enemies.Count > 0)
                    HasEnemyTracked = true;
            }

            LogVerbose($"[SPAWN] Objects inspected={inspected}, EnemyStoredNew={tracked}, EnemyCount={GetEnemyCount()}");
        }

        public void MarkObjectDead(ObjectInfo obj)
        {
            if (obj == null)
                return;

            int objectId = obj.ObjectId;
            if (GetObjectTypeById(objectId) != GameObjectType.Enemy)
                return;

            lock (_lock)
            {
                _deadEnemies.Add(objectId);
                _enemies.Remove(objectId);
            }

            LogVerbose($"Enemy marked dead. ObjectId={objectId}");
        }

        public void RemoveObjects(IEnumerable<int> objectIds)
        {
            if (objectIds == null)
                return;

            int removed = 0;
            lock (_lock)
            {
                foreach (int objectId in objectIds)
                {
                    if (GetObjectTypeById(objectId) != GameObjectType.Enemy)
                        continue;

                    _deadEnemies.Add(objectId);
                    if (_enemies.Remove(objectId))
                        removed++;
                }
            }

            if (removed > 0)
                LogVerbose($"Enemy removed by S_Despawn. Count={removed}");
        }

        public void ObserveAddItem(S_AddItem packet)
        {
            if (packet == null)
                return;

            AddItemReceivedCount++;
            if (packet.Items.Count > 0)
            {
                ItemInfo item = packet.Items[packet.Items.Count - 1];
                LastRewardTemplateId = item.TemplateId;
                LastRewardCount = item.Count;
            }

            Log($"S_AddItem observed. Items={packet.Items.Count}, LastTemplateId={LastRewardTemplateId}, LastCount={LastRewardCount}");

            if (Options.StopAfterReward)
                _stopGameplayRequested = true;
        }

        public void CompleteCreatePlayers()
        {
            if (Options.IsCreatePlayers == false)
                return;

            ScenarioCompletedAtUtc = DateTime.UtcNow;
            SetState(DummyClientState.Completed);
            _completion.TrySetResult(true);
        }

        public void BeginTownLoadIfReady()
        {
            if (Options.IsTownLoad == false)
                return;

            lock (_lock)
            {
                if (_holdStarted)
                    return;

                _holdStarted = true;
                TargetRoomId = TargetRoomId > 0 ? TargetRoomId : 1;
                TargetRoomType = RoomType.Town;
                State = DummyClientState.HoldingConnection;
            }

            RegisterKnownObject(PlayerInfo?.ObjectId ?? 0, Name, TargetRoomId);
            CaptureGameplayOrigin();
            Task.Run(RunTownLoadLoopAsync);
        }

        private async Task RunTownLoadLoopAsync()
        {
            try
            {
                if (Options.GameplayStartDelayMs > 0)
                    await Task.Delay(Options.GameplayStartDelayMs);

                if (State == DummyClientState.Failed || HasEnteredTown == false)
                    return;

                HasGameplayStarted = Options.EnableMovement;
                TownLoadStartedAtUtc = DateTime.UtcNow;
                GameplayStartedAtUtc = TownLoadStartedAtUtc;
                if (Options.EnableMovement)
                    SetState(DummyClientState.GameplayStarted);

                Log($"Town load started. RoomId={TargetRoomId}, Movement={Options.EnableMovement}, Pattern={Options.MovementPattern}, HoldInTownSec={Options.HoldInTownSec}, Bounds={DescribeDungeonBounds()}");

                DateTime startedAt = DateTime.UtcNow;
                DateTime nextMoveAt = DateTime.UtcNow;
                TimeSpan duration = TimeSpan.FromSeconds(Options.HoldInTownSec);

                while (State != DummyClientState.Failed && DateTime.UtcNow - startedAt < duration)
                {
                    DateTime now = DateTime.UtcNow;
                    if (Options.EnableMovement && now >= nextMoveAt)
                    {
                        SendMovement();
                        nextMoveAt = now.AddMilliseconds(Options.MovementIntervalMs);
                    }

                    await Task.Delay(25);
                }

                if (Options.EnableMovement)
                    SendIdleMove();

                HasGameplayCompleted = Options.EnableMovement;
                TownLoadCompletedAtUtc = DateTime.UtcNow;
                GameplayCompletedAtUtc = TownLoadCompletedAtUtc;
                if (Options.EnableMovement)
                    SetState(DummyClientState.GameplayCompleted);

                Log($"Town load completed. RoomId={TargetRoomId}, MoveSent={MoveSentCount}, ReceivedMove={ReceivedMoveCount}, CrossRoomMoveSuspected={CrossRoomMoveSuspectedCount}, LastPosition=({LastPosX:0.00},{LastPosY:0.00})");
                ScenarioCompletedAtUtc = DateTime.UtcNow;
                SetState(DummyClientState.Completed);
                _completion.TrySetResult(true);
            }
            catch (Exception ex)
            {
                Log($"Town load loop failed: {ex.Message}");
                Fail($"Town load loop failed: {ex.Message}");
            }
        }

        public void BeginHoldIfReady()
        {
            if (Options.IsMatchScenario == false)
                return;

            lock (_lock)
            {
                if (_holdStarted)
                    return;
                _holdStarted = true;
                State = DummyClientState.HoldingConnection;
            }

            Task.Run(async () =>
            {
                int gameplayHoldSec = Options.HasGameplayEnabled ? Options.GameplayDurationSec + Math.Max(1, Options.GameplayStartDelayMs / 1000) : 0;
                int holdSec = Math.Max(Options.HoldAfterDungeonSec, gameplayHoldSec);
                Log($"Holding connection for {holdSec} sec.");
                await Task.Delay(TimeSpan.FromSeconds(holdSec));
                SetState(DummyClientState.Completed);
                _completion.TrySetResult(true);
            });
        }

        private void CaptureGameplayOrigin()
        {
            PositionInfo posInfo = PlayerInfo?.PosInfo;
            _originX = posInfo?.PosX ?? 0f;
            _originY = posInfo?.PosY ?? 0f;

            float phaseOffset = 0f;
            if (Options.Clients > 1 && Options.MovementRadius > 0f)
                phaseOffset = ((Index % Options.Clients) - (Options.Clients - 1) / 2f) * Math.Min(0.35f, Options.MovementRadius / Math.Max(1, Options.Clients));

            _currentX = _originX + phaseOffset;
            _currentY = _originY;
            ClampCurrentToDungeonBounds();
            LastPosX = _currentX;
            LastPosY = _currentY;
            LogVerbose($"[FOLLOW] Origin captured. Player={Name}, Origin=({_originX:0.00},{_originY:0.00}), Current=({_currentX:0.00},{_currentY:0.00}), PlayerInfoObjectId={PlayerInfo?.ObjectId ?? 0}");
        }

        private void BeginGameplayIfReady()
        {
            if (Options.IsMatchScenario == false || Options.HasGameplayEnabled == false)
                return;

            lock (_lock)
            {
                if (_gameplayStarted)
                    return;
                _gameplayStarted = true;
            }

            Task.Run(RunGameplayLoopAsync);
        }

        private async Task RunGameplayLoopAsync()
        {
            try
            {
                if (Options.GameplayStartDelayMs > 0)
                    await Task.Delay(Options.GameplayStartDelayMs);

                if (State == DummyClientState.Failed || HasEnteredDungeon == false)
                    return;

                HasGameplayStarted = true;
                SetState(DummyClientState.GameplayStarted);
                double elapsedSinceDungeonMs = _enteredDungeonAtUtc == default ? 0 : (DateTime.UtcNow - _enteredDungeonAtUtc).TotalMilliseconds;
                Log($"Gameplay started. Movement={Options.EnableMovement}, Pattern={Options.MovementPattern}, Attack={Options.EnableAttack}, Collision={Options.EnableCollision}, DurationSec={Options.GameplayDurationSec}, ElapsedSinceDungeonMs={elapsedSinceDungeonMs:0}, DungeonBounds={DescribeDungeonBounds()}");

                DateTime startedAt = DateTime.UtcNow;
                DateTime nextMoveAt = DateTime.UtcNow;
                DateTime nextAttackAt = DateTime.UtcNow;
                TimeSpan duration = TimeSpan.FromSeconds(Options.GameplayDurationSec);

                while (State != DummyClientState.Failed && _stopGameplayRequested == false && DateTime.UtcNow - startedAt < duration)
                {
                    DateTime now = DateTime.UtcNow;

                    if (Options.EnableMovement && now >= nextMoveAt)
                    {
                        SendMovement();
                        nextMoveAt = now.AddMilliseconds(Options.MovementIntervalMs);
                    }

                    if (Options.EnableAttack && now >= nextAttackAt)
                    {
                        TrySendAttack();
                        nextAttackAt = now.AddMilliseconds(Options.AttackIntervalMs);
                    }

                    await Task.Delay(25);
                }

                if (Options.EnableMovement)
                    SendIdleMove();

                HasGameplayCompleted = true;
                SetState(DummyClientState.GameplayCompleted);
                Log($"Gameplay completed. MoveSent={MoveSentCount}, SkillSent={SkillSentCount}, CollisionSent={CollisionSentCount}, AddItemReceived={AddItemReceivedCount}, FollowShouldMove={FollowShouldMoveCount}, FollowIdle={FollowIdleCount}, DungeonBoundsClamp={DungeonBoundsClampCount}, LastSkillId={LastSkillId}, LastPosition=({LastPosX:0.00},{LastPosY:0.00}), TargetEnemyId={TargetEnemyObjectId}, LastTargetDistance={LastTargetDistance:0.00}, FollowReached={HasFollowReached}");
            }
            catch (Exception ex)
            {
                Log($"Gameplay loop failed: {ex.Message}");
            }
        }

        private static string ResolvePatrolMode(DummyClientOptions options, int index)
        {
            string requested = options.GetRequestedPatrolMode();
            if (string.Equals(requested, "mixed", StringComparison.OrdinalIgnoreCase))
            {
                switch (index % 3)
                {
                    case 0:
                        return "horizontal";
                    case 1:
                        return "vertical";
                    default:
                        return "box";
                }
            }

            if (string.Equals(requested, "diagonal", StringComparison.OrdinalIgnoreCase))
                return "diagonal";

            if (string.Equals(requested, "box", StringComparison.OrdinalIgnoreCase))
                return "box";

            if (string.Equals(requested, "vertical", StringComparison.OrdinalIgnoreCase))
                return "vertical";

            return "horizontal";
        }
        private void SendMovement()
        {
            if (Options.IsFollowEnemyMovement)
            {
                SendFollowEnemyMove();
                return;
            }

            SendPatrolMove();
        }

        private void SendPatrolMove()
        {
            switch (_patrolMode)
            {
                case "vertical":
                    SendVerticalPatrolMove();
                    break;
                case "diagonal":
                    SendDiagonalPatrolMove();
                    break;
                case "box":
                    SendBoxPatrolMove();
                    break;
                case "horizontal":
                default:
                    SendHorizontalPatrolMove();
                    break;
            }
        }

        private void SendHorizontalPatrolMove()
        {
            float intervalSec = Math.Max(0.001f, Options.MovementIntervalMs / 1000f);
            float delta = Options.MovementSpeed * intervalSec * _patrolXDirection;
            _currentX += delta;

            float minX = GetPatrolMinX();
            float maxX = GetPatrolMaxX();

            if (_currentX > maxX)
            {
                _currentX = maxX;
                _patrolXDirection = -1;
            }
            else if (_currentX < minX)
            {
                _currentX = minX;
                _patrolXDirection = 1;
            }

            MoveDir moveDir = _patrolXDirection >= 0 ? MoveDir.Right : MoveDir.Left;
            SendPatrolMovePacket("Horizontal", moveDir);
        }

        private void SendVerticalPatrolMove()
        {
            float intervalSec = Math.Max(0.001f, Options.MovementIntervalMs / 1000f);
            float delta = Options.MovementSpeed * intervalSec * _patrolYDirection;
            _currentY += delta;

            float minY = GetPatrolMinY();
            float maxY = GetPatrolMaxY();

            if (_currentY > maxY)
            {
                _currentY = maxY;
                _patrolYDirection = -1;
            }
            else if (_currentY < minY)
            {
                _currentY = minY;
                _patrolYDirection = 1;
            }

            MoveDir moveDir = _patrolYDirection >= 0 ? MoveDir.Up : MoveDir.Down;
            SendPatrolMovePacket("Vertical", moveDir);
        }

        private void SendDiagonalPatrolMove()
        {
            float intervalSec = Math.Max(0.001f, Options.MovementIntervalMs / 1000f);
            float step = Options.MovementSpeed * intervalSec;
            _currentX += step * _patrolXDirection;
            _currentY += step * _patrolYDirection;

            ClampPatrolX();
            ClampPatrolY();

            MoveDir moveDir = Math.Abs(_patrolXDirection) >= Math.Abs(_patrolYDirection)
                ? (_patrolXDirection >= 0 ? MoveDir.Right : MoveDir.Left)
                : (_patrolYDirection >= 0 ? MoveDir.Up : MoveDir.Down);
            SendPatrolMovePacket("Diagonal", moveDir);
        }

        private void SendBoxPatrolMove()
        {
            float intervalSec = Math.Max(0.001f, Options.MovementIntervalMs / 1000f);
            float step = Options.MovementSpeed * intervalSec;
            MoveDir moveDir;

            switch (_patrolBoxSegment)
            {
                case 0:
                    _currentX += step;
                    if (_currentX >= GetPatrolMaxX())
                    {
                        _currentX = GetPatrolMaxX();
                        _patrolBoxSegment = 1;
                    }
                    moveDir = MoveDir.Right;
                    break;
                case 1:
                    _currentY += step;
                    if (_currentY >= GetPatrolMaxY())
                    {
                        _currentY = GetPatrolMaxY();
                        _patrolBoxSegment = 2;
                    }
                    moveDir = MoveDir.Up;
                    break;
                case 2:
                    _currentX -= step;
                    if (_currentX <= GetPatrolMinX())
                    {
                        _currentX = GetPatrolMinX();
                        _patrolBoxSegment = 3;
                    }
                    moveDir = MoveDir.Left;
                    break;
                default:
                    _currentY -= step;
                    if (_currentY <= GetPatrolMinY())
                    {
                        _currentY = GetPatrolMinY();
                        _patrolBoxSegment = 0;
                    }
                    moveDir = MoveDir.Down;
                    break;
            }

            SendPatrolMovePacket("Box", moveDir);
        }

        private void ClampPatrolX()
        {
            float minX = GetPatrolMinX();
            float maxX = GetPatrolMaxX();

            if (_currentX > maxX)
            {
                _currentX = maxX;
                _patrolXDirection = -1;
            }
            else if (_currentX < minX)
            {
                _currentX = minX;
                _patrolXDirection = 1;
            }
        }

        private void ClampPatrolY()
        {
            float minY = GetPatrolMinY();
            float maxY = GetPatrolMaxY();

            if (_currentY > maxY)
            {
                _currentY = maxY;
                _patrolYDirection = -1;
            }
            else if (_currentY < minY)
            {
                _currentY = minY;
                _patrolYDirection = 1;
            }
        }
        private bool ShouldApplyDungeonMovementBounds()
        {
            return Options.UseDungeonMovementBounds && ((Options.IsMatchScenario && HasEnteredDungeon) || (Options.IsTownLoad && HasEnteredTown));
        }

        private string DescribeDungeonBounds()
        {
            if (!ShouldApplyDungeonMovementBounds())
                return "Disabled";

            if (DummyMovementBoundsProvider.TryGet(TargetRoomType, out DummyMovementBoundsMap map))
                return $"Tilemap[{map.SourceTilemap}, Basis={map.CoordinateBasis}, Cells={map.HasTileCount}, Spans={map.WalkableSpans?.Count ?? 0}, Bounds={map.MinX:0.00},{map.MinY:0.00}..{map.MaxX:0.00},{map.MaxY:0.00}]";

            return $"Fallback[{GetFallbackMinX():0.00},{GetFallbackMinY():0.00}..{GetFallbackMaxX():0.00},{GetFallbackMaxY():0.00}]";
        }

        private float GetPatrolMinX()
        {
            float value = _originX - Options.MovementRadius;
            if (!ShouldApplyDungeonMovementBounds())
                return value;
            if (DummyMovementBoundsProvider.TryGet(TargetRoomType, out DummyMovementBoundsMap map))
                return Math.Max(value, map.MinX);
            return Math.Max(value, GetFallbackMinX());
        }

        private float GetPatrolMaxX()
        {
            float value = _originX + Options.MovementRadius;
            if (!ShouldApplyDungeonMovementBounds())
                return value;
            if (DummyMovementBoundsProvider.TryGet(TargetRoomType, out DummyMovementBoundsMap map))
                return Math.Min(value, map.MaxX);
            return Math.Min(value, GetFallbackMaxX());
        }

        private float GetPatrolMinY()
        {
            float value = _originY - Options.MovementRadius;
            if (!ShouldApplyDungeonMovementBounds())
                return value;
            if (DummyMovementBoundsProvider.TryGet(TargetRoomType, out DummyMovementBoundsMap map))
                return Math.Max(value, map.MinY);
            return Math.Max(value, GetFallbackMinY());
        }

        private float GetPatrolMaxY()
        {
            float value = _originY + Options.MovementRadius;
            if (!ShouldApplyDungeonMovementBounds())
                return value;
            if (DummyMovementBoundsProvider.TryGet(TargetRoomType, out DummyMovementBoundsMap map))
                return Math.Min(value, map.MaxY);
            return Math.Min(value, GetFallbackMaxY());
        }

        private float GetFallbackMinX()
        {
            return TargetRoomType == RoomType.Town ? TownLobbyFallbackMinX : Options.DungeonMinX;
        }

        private float GetFallbackMaxX()
        {
            return TargetRoomType == RoomType.Town ? TownLobbyFallbackMaxX : Options.DungeonMaxX;
        }

        private float GetFallbackMinY()
        {
            return TargetRoomType == RoomType.Town ? TownLobbyFallbackMinY : Options.DungeonMinY;
        }

        private float GetFallbackMaxY()
        {
            return TargetRoomType == RoomType.Town ? TownLobbyFallbackMaxY : Options.DungeonMaxY;
        }
        private bool ClampCurrentToDungeonBounds()
        {
            if (!ShouldApplyDungeonMovementBounds())
                return false;

            if (DummyMovementBoundsProvider.TryGet(TargetRoomType, out DummyMovementBoundsMap map) &&
                map.TryClamp(_currentX, _currentY, out float mapX, out float mapY))
            {
                bool mapClamped = Math.Abs(mapX - _currentX) > 0.001f || Math.Abs(mapY - _currentY) > 0.001f;
                if (!mapClamped)
                    return false;

                _currentX = mapX;
                _currentY = mapY;
                DungeonBoundsClampCount++;
                return true;
            }

            float clampedX = Math.Max(GetFallbackMinX(), Math.Min(GetFallbackMaxX(), _currentX));
            float clampedY = Math.Max(GetFallbackMinY(), Math.Min(GetFallbackMaxY(), _currentY));
            bool clamped = Math.Abs(clampedX - _currentX) > 0.001f || Math.Abs(clampedY - _currentY) > 0.001f;
            if (!clamped)
                return false;

            _currentX = clampedX;
            _currentY = clampedY;
            DungeonBoundsClampCount++;
            return true;
        }
        private void SendPatrolMovePacket(string pattern, MoveDir moveDir)
        {
            LogVerbose($"[DUMMY][PATROL] Name={Name}, RoomId={TargetRoomId}, Pattern={pattern}, Pos=({_currentX:0.00},{_currentY:0.00}), Dir={moveDir}, State={PlayerState.Moving}");
            SendMove(_currentX, _currentY, PlayerState.Moving, moveDir);
        }

        private void SendFollowEnemyMove()
        {
            if (TrySelectTargetEnemy(out ObjectInfo target) == false)
            {
                if (_enemyMissingLogged == false)
                {
                    _enemyMissingLogged = true;
                    Log($"[FOLLOW] EnemyTracked={HasEnemyTracked}, EnemyCount={GetEnemyCount()}, Reason=NoEnemy");
                }
                return;
            }

            PositionInfo targetPos = target.PosInfo;
            float dx = targetPos.PosX - _currentX;
            float dy = targetPos.PosY - _currentY;
            float dist = (float)Math.Sqrt(dx * dx + dy * dy);
            float arrivalDistance = Options.FollowStopDistance + Options.FollowArrivalEpsilon;

            TargetEnemyObjectId = target.ObjectId;
            LastTargetX = targetPos.PosX;
            LastTargetY = targetPos.PosY;
            LastTargetDistance = dist;

            if (_lastFollowTargetObjectId != target.ObjectId)
            {
                _lastFollowTargetObjectId = target.ObjectId;
                _followArrivalIdleSent = false;
                _lastInRange = false;
            }

            if (dist <= arrivalDistance || dist <= 0.001f)
            {
                MarkFollowArrived();

                if (Options.IsFollowInRangeStrafe)
                {
                    SendInRangeStrafeMove(target);
                    return;
                }

                HandleFollowIdleArrival(dist, target.ObjectId, "WithinStopDistance");
                return;
            }

            float idleResetDistance = arrivalDistance + Options.FollowArrivalEpsilon;
            if (dist > idleResetDistance)
            {
                _lastInRange = false;
                _followArrivalIdleSent = false;
            }

            float intervalSec = Math.Max(0.001f, Options.MovementIntervalMs / 1000f);
            float step = Options.MovementSpeed * intervalSec;
            float maxStep = Math.Max(0f, dist - Options.FollowStopDistance);
            if (step > maxStep)
                step = maxStep;

            if (step <= 0f)
            {
                MarkFollowArrived();
                HandleFollowIdleArrival(dist, target.ObjectId, "StepZero");
                return;
            }

            float nextX = _currentX + dx / dist * step;
            float nextY = _currentY + dy / dist * step;
            FollowShouldMoveCount++;
            LogVerbose($"[FOLLOW] Player={Name}, EnemyTracked={HasEnemyTracked}, TargetId={target.ObjectId}, Current=({_currentX:0.00},{_currentY:0.00}), Target=({targetPos.PosX:0.00},{targetPos.PosY:0.00}), Dx={dx:0.00}, Dy={dy:0.00}, Dist={dist:0.00}, Stop={Options.FollowStopDistance:0.00}, Step={step:0.00}, ShouldMove=True, Next=({nextX:0.00},{nextY:0.00})");

            _currentX = nextX;
            _currentY = nextY;

            MoveDir moveDir = dx >= 0f ? MoveDir.Right : MoveDir.Left;
            SendMove(_currentX, _currentY, PlayerState.Moving, moveDir);
        }


        private void HandleFollowIdleArrival(float distance, int targetObjectId, string reason)
        {
            FollowIdleCount++;
            LogVerbose($"[FOLLOW][ARRIVAL] Name={Name}, TargetId={targetObjectId}, Dist={distance:0.00}, Stop={Options.FollowStopDistance:0.00}, Epsilon={Options.FollowArrivalEpsilon:0.00}, Mode={Options.FollowInRangeMode}, Reason={reason}, IdleSent={!_followArrivalIdleSent}");

            if (_followArrivalIdleSent)
            {
                LogVerbose($"[FOLLOW][ARRIVAL_SKIP] Name={Name}, Reason=AlreadyIdleSent, LastState={_lastMoveState}");
                return;
            }

            if (Options.SendIdleOnArrival)
            {
                SendMove(_currentX, _currentY, PlayerState.Idle, MoveDir.None);
                IdleOnArrivalSentCount++;
            }
            else
            {
                _lastMoveState = PlayerState.Idle;
            }

            _followArrivalIdleSent = true;
        }
        private void MarkFollowArrived()
        {
            if (HasFollowReached == false)
                FollowArrivedCount++;

            HasFollowReached = true;
            _lastInRange = true;
        }

        private void SendIdleOnArrivalIfNeeded()
        {
            if (Options.SendIdleOnArrival == false)
                return;

            if (_followArrivalIdleSent && _lastMoveState == PlayerState.Idle)
                return;

            SendMove(_currentX, _currentY, PlayerState.Idle, MoveDir.None);
            _followArrivalIdleSent = true;
            IdleOnArrivalSentCount++;
            LogVerbose($"[FOLLOW] IdleOnArrival sent. Player={Name}, Pos=({_currentX:0.00},{_currentY:0.00}), LastState={_lastMoveState}");
        }

        private void SendInRangeStrafeMove(ObjectInfo target)
        {
            PositionInfo targetPos = target?.PosInfo;
            if (targetPos == null)
            {
                SendIdleOnArrivalIfNeeded();
                return;
            }

            DateTime now = DateTime.UtcNow;
            if (_nextStrafeSwitchAtUtc == default || now >= _nextStrafeSwitchAtUtc)
            {
                _strafeDirection *= -1;
                _nextStrafeSwitchAtUtc = now.AddMilliseconds(Options.StrafeSwitchMs + Index * 75);
            }

            float phaseOffset = (Index - (Options.Clients - 1) / 2f) * 0.08f;
            float desiredX = targetPos.PosX + _strafeDirection * Options.StrafeDistance;
            float desiredY = targetPos.PosY + phaseOffset;
            float dx = desiredX - _currentX;
            float dy = desiredY - _currentY;
            float dist = (float)Math.Sqrt(dx * dx + dy * dy);

            if (dist <= Options.FollowArrivalEpsilon || dist <= 0.001f)
            {
                FollowIdleCount++;
                SendMove(_currentX, _currentY, PlayerState.Idle, MoveDir.None);
                return;
            }

            float intervalSec = Math.Max(0.001f, Options.MovementIntervalMs / 1000f);
            float step = Math.Min(Options.MovementSpeed * intervalSec, dist);
            _currentX += dx / dist * step;
            _currentY += dy / dist * step;

            MoveDir moveDir = dx >= 0f ? MoveDir.Right : MoveDir.Left;
            StrafeMoveSentCount++;
            FollowShouldMoveCount++;
            LogVerbose($"[FOLLOW] Strafe. Player={Name}, TargetId={target.ObjectId}, Desired=({desiredX:0.00},{desiredY:0.00}), Next=({_currentX:0.00},{_currentY:0.00}), Dist={dist:0.00}, Dir={moveDir}");
            SendMove(_currentX, _currentY, PlayerState.Moving, moveDir);
        }

        private MoveDir BouncePatrolDirectionAfterBoundsClamp(float requestedX, float requestedY, float acceptedX, float acceptedY, MoveDir fallback)
        {
            float dx = requestedX - acceptedX;
            float dy = requestedY - acceptedY;
            bool clampedX = Math.Abs(dx) > 0.001f;
            bool clampedY = Math.Abs(dy) > 0.001f;

            if (clampedX)
                _patrolXDirection *= -1;

            if (clampedY)
                _patrolYDirection *= -1;

            if (clampedX || clampedY)
                AdvanceBoxSegmentAfterBoundsClamp(clampedX, clampedY);

            if (clampedX && Math.Abs(dx) >= Math.Abs(dy))
                return dx > 0f ? MoveDir.Left : MoveDir.Right;

            if (clampedY)
                return dy > 0f ? MoveDir.Down : MoveDir.Up;

            return fallback;
        }

        private void AdvanceBoxSegmentAfterBoundsClamp(bool clampedX, bool clampedY)
        {
            if (!string.Equals(_patrolMode, "box", StringComparison.OrdinalIgnoreCase))
                return;

            if (clampedX && _patrolBoxSegment == 0)
                _patrolBoxSegment = 1;
            else if (clampedY && _patrolBoxSegment == 1)
                _patrolBoxSegment = 2;
            else if (clampedX && _patrolBoxSegment == 2)
                _patrolBoxSegment = 3;
            else if (clampedY && _patrolBoxSegment == 3)
                _patrolBoxSegment = 0;
        }
        private void SendIdleMove()
        {
            SendMove(_currentX, _currentY, PlayerState.Idle, MoveDir.None);
        }

        private void SendMove(float x, float y, PlayerState state, MoveDir moveDir)
        {
            _currentX = x;
            _currentY = y;
            float requestedX = _currentX;
            float requestedY = _currentY;
            bool clamped = ClampCurrentToDungeonBounds();
            x = _currentX;
            y = _currentY;
            if (clamped)
            {
                moveDir = BouncePatrolDirectionAfterBoundsClamp(requestedX, requestedY, x, y, moveDir);
                LogVerbose($"[DUMMY][BOUNDS] Name={Name}, RoomId={TargetRoomId}, RoomType={TargetRoomType}, Requested=({requestedX:0.00},{requestedY:0.00}), Pos=({x:0.00},{y:0.00}), MoveDir={moveDir}, Bounds={DescribeDungeonBounds()}, ClampCount={DungeonBoundsClampCount}");
            }

            C_Move movePacket = new C_Move
            {
                PosInfo = new PositionInfo
                {
                    PosX = x,
                    PosY = y,
                    State = state,
                    MoveDir = moveDir
                }
            };

            Session.Send(movePacket);
            MoveSentCount++;
            LastPosX = x;
            LastPosY = y;
            _lastMoveState = state;
            LogVerbose($"[MOVE] Player={Name}, Sent C_Move Pos=({x:0.00},{y:0.00}), State={state}, MoveDir={moveDir}, MoveSent={MoveSentCount}");
        }

        private void TrySendAttack()
        {
            ObjectInfo target = null;
            bool hasTarget = TrySelectTargetEnemy(out target);
            bool targetInRange = hasTarget && IsTargetInAttackRange(target);

            if (Options.AttackOnlyInRange && targetInRange == false)
            {
                LogVerbose($"C_Skill skipped. Target enemy is out of range or missing. Distance={LastTargetDistance:0.00}, AttackRange={Options.AttackRange:0.00}");
                return;
            }

            int skillId = SendNextSkill();
            if (skillId <= 0 || Options.EnableCollision == false)
                return;

            if (hasTarget == false)
            {
                CollisionSkippedNoTargetCount++;
                return;
            }

            if (Options.CollisionOnlyInRange && targetInRange == false)
            {
                CollisionSkippedOutOfRangeCount++;
                return;
            }

            ScheduleCollision(target.Clone(), skillId);
        }

        private bool IsTargetInAttackRange(ObjectInfo target)
        {
            if (target?.PosInfo == null)
                return false;

            float dx = target.PosInfo.PosX - _currentX;
            float dy = target.PosInfo.PosY - _currentY;
            float dist = (float)Math.Sqrt(dx * dx + dy * dy);

            TargetEnemyObjectId = target.ObjectId;
            LastTargetX = target.PosInfo.PosX;
            LastTargetY = target.PosInfo.PosY;
            LastTargetDistance = dist;

            return dist <= Options.AttackRange;
        }

        private int SendNextSkill()
        {
            if (Options.SkillIds.Count == 0)
                return 0;

            int skillId = Options.SkillIds[_nextSkillIndex % Options.SkillIds.Count];
            _nextSkillIndex++;

            C_Skill skillPacket = new C_Skill
            {
                Info = new SkillInfo
                {
                    SkillId = skillId
                }
            };

            Session.Send(skillPacket);
            SkillSentCount++;
            LastSkillId = skillId;
            LogVerbose($"C_Skill sent. SkillId={skillId}");
            return skillId;
        }

        private void ScheduleCollision(ObjectInfo target, int skillId)
        {
            Task.Run(async () =>
            {
                try
                {
                    if (Options.CollisionDelayMs > 0)
                        await Task.Delay(Options.CollisionDelayMs);

                    SendCollisionIfValid(target, skillId);
                }
                catch (Exception ex)
                {
                    Log($"C_Collision schedule failed. TargetId={target?.ObjectId ?? 0}, SkillId={skillId}, Error={ex.Message}");
                }
            });
        }

        private void SendCollisionIfValid(ObjectInfo target, int skillId)
        {
            if (target == null)
            {
                CollisionSkippedNoTargetCount++;
                return;
            }

            if (State == DummyClientState.Failed || HasEnteredDungeon == false)
                return;

            int targetId = target.ObjectId;
            lock (_lock)
            {
                if (_deadEnemies.Contains(targetId) || _enemies.ContainsKey(targetId) == false)
                {
                    CollisionSkippedDeadTargetCount++;
                    return;
                }

                if (Options.MaxCollisionsPerTarget > 0)
                {
                    _collisionCountByTarget.TryGetValue(targetId, out int count);
                    if (count >= Options.MaxCollisionsPerTarget)
                    {
                        CollisionSkippedLimitCount++;
                        return;
                    }

                    _collisionCountByTarget[targetId] = count + 1;
                }
                else
                {
                    _collisionCountByTarget.TryGetValue(targetId, out int count);
                    _collisionCountByTarget[targetId] = count + 1;
                }
            }

            if (Options.CollisionOnlyInRange && IsTargetInAttackRange(target) == false)
            {
                CollisionSkippedOutOfRangeCount++;
                return;
            }

            C_Collision collisionPacket = new C_Collision
            {
                Playerinfo = target.Clone()
            };

            Session.Send(collisionPacket);
            CollisionSentCount++;
            LastCollisionTargetId = targetId;
            LogVerbose($"C_Collision sent. TargetId={targetId}, SkillId={skillId}, CollisionCount={CollisionSentCount}");
        }

        private bool TrySelectTargetEnemy(out ObjectInfo target)
        {
            lock (_lock)
            {
                target = null;
                if (_enemies.Count == 0)
                    return false;

                if (Options.UseFirstEnemy)
                {
                    target = _enemies.OrderBy(pair => pair.Key).Select(pair => pair.Value).FirstOrDefault(e => e?.PosInfo != null && _deadEnemies.Contains(e.ObjectId) == false);
                    return target != null;
                }

                float bestDistSq = float.MaxValue;
                foreach (ObjectInfo enemy in _enemies.Values)
                {
                    if (enemy?.PosInfo == null || _deadEnemies.Contains(enemy.ObjectId))
                        continue;

                    float dx = enemy.PosInfo.PosX - _currentX;
                    float dy = enemy.PosInfo.PosY - _currentY;
                    float distSq = dx * dx + dy * dy;
                    if (distSq < bestDistSq)
                    {
                        bestDistSq = distSq;
                        target = enemy;
                    }
                }

                return target != null;
            }
        }

        private int GetEnemyCount()
        {
            lock (_lock)
            {
                return _enemies.Count;
            }
        }

        private static GameObjectType GetObjectTypeById(int objectId)
        {
            int type = (objectId >> 24) & 0x7F;
            return (GameObjectType)type;
        }


        public void ObserveMove(S_Move packet)
        {
            if (packet == null)
                return;

            ReceivedMoveCount++;

            int moverRoomId = 0;
            string moverName = null;
            lock (RoomTrackLock)
            {
                RoomIdByObjectId.TryGetValue(packet.PlayerId, out moverRoomId);
                NameByObjectId.TryGetValue(packet.PlayerId, out moverName);
            }

            PositionInfo pos = packet.PosInfo;
            LogVerbose($"[DUMMY][S_MOVE_RECV] Receiver={Name}, ReceiverRoomId={TargetRoomId}, Mover={moverName ?? "Unknown"}, MoverObjectId={packet.PlayerId}, MoverRoomId={(moverRoomId > 0 ? moverRoomId.ToString() : "Unknown")}, PacketPos=({pos?.PosX ?? 0f:0.00},{pos?.PosY ?? 0f:0.00}), State={pos?.State}, Dir={pos?.MoveDir}");

            if (TargetRoomId > 0 && moverRoomId > 0 && moverRoomId != TargetRoomId)
            {
                CrossRoomMoveSuspectedCount++;
                Log($"[DUMMY][ROOM_ISOLATION_WARNING] Receiver={Name}, ReceiverRoomId={TargetRoomId}, Mover={moverName ?? "Unknown"}, MoverObjectId={packet.PlayerId}, MoverRoomId={moverRoomId}");
            }
        }

        private static void RegisterKnownObject(int objectId, string name, int roomId)
        {
            if (objectId <= 0 || roomId <= 0)
                return;

            lock (RoomTrackLock)
            {
                RoomIdByObjectId[objectId] = roomId;
                if (string.IsNullOrWhiteSpace(name) == false)
                    NameByObjectId[objectId] = name;
            }
        }
        public void Fail(string reason)
        {
            SetState(DummyClientState.Failed);
            Log($"FAILED: {reason}");
            _completion.TrySetResult(false);
        }

        public void Log(string message)
        {
            Console.WriteLine($"[DUMMY:{Name}] {message}");
        }

        public void LogVerbose(string message)
        {
            if (Options.Verbose)
                Log(message);
        }
    }
}





















