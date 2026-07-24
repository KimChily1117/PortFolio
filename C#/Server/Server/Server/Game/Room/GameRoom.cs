using Google.Protobuf;
using Google.Protobuf.Protocol;
using Microsoft.EntityFrameworkCore.Internal;
using Server.DB;
using Server.Game.Object;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;

namespace Server.Game.Room
{
    public partial class GameRoom : JobSerializer
    {
        private static readonly bool DebugSpawnBroadcastLog = false;
        private const float BakalEnemySpawnX = -2f;
        private const float BakalEnemySpawnY = -0.55f;
        private const double SlowUpdateThresholdMs = 30.0;
        private const int DungeonClearReturnDelayMs = 5000;
        public const float TownAoiEnterRadius = 15f;
        public const float TownAoiExitRadius = 17f;

        object _lock = new object();
        public int RoomId { get; set; }
        public RoomType RoomType { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime LastUpdatedAtUtc { get; set; }
        public string State { get; set; } = "Running";
        public long UpdateCount { get; private set; }
        public double LastUpdateMs { get; private set; }
        public double MaxUpdateMs { get; private set; }
        public int PlayerCount { get { lock (_lock) { return _players.Count; } } }
        public int EnemyCount { get { lock (_lock) { return _enemys.Count; } } }
        public bool IsEmpty { get { return PlayerCount == 0; } }
        public bool IsDungeonRoom { get { return RoomType != Google.Protobuf.Protocol.RoomType.Town; } }

        Dictionary<int, Player> _players = new Dictionary<int, Player>();
        Dictionary<int, Enemy> _enemys = new Dictionary<int, Enemy>();
        Dictionary<int, Projectile> _Projectiles = new Dictionary<int, Projectile>();
        Dictionary<int, HashSet<int>> _visiblePlayersByObserver = new Dictionary<int, HashSet<int>>();

        Dictionary<int,LobbyPlayerInfo> _PartyplayerList = new Dictionary<int,LobbyPlayerInfo>();
        bool _dungeonClearHandled;

        public List<Player> GetPlayersSnapshot()
        {
            lock (_lock)
            {
                return _players.Values.Where(player => player != null).ToList();
            }
        }

        public void HandleDungeonClear(Enemy clearedEnemy)
        {
            if (RoomType != Google.Protobuf.Protocol.RoomType.Bakal)
                return;

            lock (_lock)
            {
                if (_dungeonClearHandled)
                    return;

                _dungeonClearHandled = true;
                State = "Cleared";
            }

            S_DungeonClear dungeonClear = new S_DungeonClear
            {
                ClearedRoomId = RoomId,
                Message = "클리어 하였습니다. 마을로 5초뒤 이동합니다.",
                ReturnDelaySeconds = DungeonClearReturnDelayMs / 1000,
                TargetRoomType = Google.Protobuf.Protocol.RoomType.Town,
                SceneType = Google.Protobuf.Protocol.SceneType.SceneTown
            };

            Console.WriteLine($"[DUNGEON_CLEAR] Bakal cleared. RoomId={RoomId}, EnemyId={clearedEnemy?.Id ?? 0}, ReturnDelayMs={DungeonClearReturnDelayMs}, PlayerCount={PlayerCount}");
            Broadcast(dungeonClear);
            PushAfter(DungeonClearReturnDelayMs, () => RoomTransferService.Instance.StartReturnToTownTransfer(this));
        }
        public RoomSnapshot CreateSnapshot()
        {
            DateTime nowUtc = DateTime.UtcNow;
            RoomSnapshot snapshot = new RoomSnapshot
            {
                RoomId = RoomId,
                RoomType = RoomType.ToString(),
                State = State,
                CreatedAtUtc = CreatedAtUtc,
                LastUpdatedAtUtc = LastUpdatedAtUtc,
                ElapsedSeconds = Math.Max(0, (nowUtc - CreatedAtUtc).TotalSeconds),
                IsDungeonRoom = IsDungeonRoom,
                UpdateCount = UpdateCount,
                LastUpdateMs = LastUpdateMs,
                MaxUpdateMs = MaxUpdateMs
            };

            lock (_lock)
            {
                snapshot.PlayerCount = _players.Count;
                snapshot.EnemyCount = _enemys.Count;
                snapshot.IsEmpty = _players.Count == 0;

                foreach (Player player in _players.Values)
                {
                    if (player == null || player.Info == null)
                        continue;

                    PositionInfo posInfo = player.Info.PosInfo;
                    snapshot.Players.Add(new RoomPlayerSnapshot
                    {
                        ObjectId = player.Id,
                        PlayerDbId = player.PlayerDbId,
                        Name = player.Info.Name,
                        Hp = ToSnapshotInt(player.HP),
                        MaxHp = ToSnapshotInt(player.MaxHP),
                        PosX = posInfo?.PosX ?? 0f,
                        PosY = posInfo?.PosY ?? 0f,
                        State = posInfo?.State.ToString() ?? player.CurrentPlayerState.ToString()
                    });
                }

                foreach (Enemy enemy in _enemys.Values)
                {
                    if (enemy == null || enemy.Info == null)
                        continue;

                    PositionInfo posInfo = enemy.Info.PosInfo;
                    RoomEnemySnapshot enemySnapshot = new RoomEnemySnapshot
                    {
                        ObjectId = enemy.Id,
                        TemplateId = enemy.TemplateId,
                        Name = enemy.Info.Name,
                        Hp = ToSnapshotInt(enemy.HP),
                        MaxHp = ToSnapshotInt(enemy.MaxHP),
                        PosX = posInfo?.PosX ?? 0f,
                        PosY = posInfo?.PosY ?? 0f,
                        IsDead = enemy.IsDead
                    };
                    snapshot.Enemies.Add(enemySnapshot);
                }

                RoomEnemySnapshot boss = snapshot.Enemies.FirstOrDefault(e => string.Equals(e.Name, "Bakal_2Phase", StringComparison.OrdinalIgnoreCase));
                if (boss == null && RoomType == Google.Protobuf.Protocol.RoomType.Bakal)
                    boss = snapshot.Enemies.FirstOrDefault();

                if (boss != null)
                {
                    snapshot.BossObjectId = boss.ObjectId;
                    snapshot.BossName = boss.Name;
                    snapshot.BossHp = boss.Hp;
                    snapshot.BossMaxHp = boss.MaxHp;
                    snapshot.BossIsDead = boss.IsDead;
                }
            }

            return snapshot;
        }

        private static int ToSnapshotInt(float value)
        {
            if (value <= 0f)
                return 0;

            return (int)Math.Ceiling(value);
        }
        public void InitEnemy()
        {
            Enemy enemy = ObjectManager.Instance.Add<Enemy>();


            enemy.Init(1);
            enemy.Info.Name = "Bakal_2Phase";
            enemy.Info.PosInfo.PosX = BakalEnemySpawnX;
            enemy.Info.PosInfo.PosY = BakalEnemySpawnY;

            Console.WriteLine($"[SPAWN] Enemy created. RoomId={RoomId}, EnemyId={enemy.Id}, TemplateId={enemy.TemplateId}, Name={enemy.Info.Name}, RootPos=({enemy.Info.PosInfo.PosX:0.00},{enemy.Info.PosInfo.PosY:0.00}), HitCenter=({enemy.HitCenterX:0.00},{enemy.HitCenterY:0.00}), CombatAnchor=({enemy.CombatAnchorX:0.00},{enemy.CombatAnchorY:0.00}), CombatLineY={enemy.CombatAnchorY:0.00}, HitRadius={enemy.HitRadius:0.00}");
            EnterRoom(enemy);
        }
        public void Update()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                foreach (Enemy enemy in _enemys.Values)
                {
                    enemy.Update();
                }

                Flush();
            }
            finally
            {
                stopwatch.Stop();
                UpdateCount++;
                LastUpdateMs = stopwatch.Elapsed.TotalMilliseconds;
                LastUpdatedAtUtc = DateTime.UtcNow;
                if (LastUpdateMs > MaxUpdateMs)
                    MaxUpdateMs = LastUpdateMs;

                if (LastUpdateMs >= SlowUpdateThresholdMs)
                    Console.WriteLine($"[ROOM][SLOW_UPDATE] RoomId={RoomId}, RoomType={RoomType}, UpdateMs={LastUpdateMs:0.00}, PlayerCount={PlayerCount}, EnemyCount={EnemyCount}");
            }
        }
        #region EnterRoom
        public void EnterRoom(GameObject gameObject)
        {
            if (gameObject == null)
                return;

            GameObjectType type = ObjectManager.GetObjectTypebyId(gameObject.Id);
            string roomType = GetRoomTypeForLog();

            bool releaseTownChannelReservation = false;

            lock (_lock)
            {

                if (type == GameObjectType.Player)
                {
                    Player player = gameObject as Player;
                    Console.WriteLine($"[ROOM] Enter Player. RoomId={RoomId}, RoomType={roomType}, PlayerId={player.Id}, ObjectId={player.Info.ObjectId}, Name={player.Info.Name}");

                    if (_players.ContainsKey(gameObject.Id))
                    {
                        _players.Remove(gameObject.Id);
                    }
                    _players.Add(gameObject.Id, player);
                    if (RoomType == Google.Protobuf.Protocol.RoomType.Town)
                    {
                        _visiblePlayersByObserver[player.Id] = new HashSet<int>();
                        foreach (Player other in _players.Values)
                        {
                            if (other == null || other == player)
                                continue;

                            if (IsWithinTownAoi(player, other, TownAoiEnterRadius))
                            {
                                TrackVisibleLocked(player.Id, other.Id);
                                TrackVisibleLocked(other.Id, player.Id);
                            }
                        }

                        releaseTownChannelReservation = true;
                    }
                    player.Room = this;
                    player.RefreshAdditionalStat();
                    LobbyPlayerInfo playerInfo = new LobbyPlayerInfo()
                    {
                        Name = player.Info.Name
                    };

                    //_PartyplayerList.Add(playerInfo);


                    // 본인한테 정보 전송
                    {
                        S_EnterGame enterPacket = new S_EnterGame();
                        enterPacket.Player = player.Info;
                        Console.WriteLine($"[SPAWN_SYNC][SERVER_SELF] RoomId={RoomId}, RoomType={RoomType}, RecipientSessionId={player.Session?.SessionId ?? 0}, RecipientPlayerId={player.Id}, Object={FormatSpawnSyncObject(player.Info)}");
                        player.Session.Send(enterPacket);

                        S_Spawn spawnPacket = new S_Spawn();

                        foreach (Player p in _players.Values)
                        {
                            if (player != p && ShouldSendInitialSpawn(player, p))
                                spawnPacket.Objects.Add(p.Info);
                        }

                        foreach (Enemy e in _enemys.Values)
                        {
                            Console.WriteLine($"[SPAWN] S_Spawn enemy. RoomId={RoomId}, EnemyId={e.Id}, ObjectId={e.Info.ObjectId}, Name={e.Info.Name}, RootPos=({e.Info.PosInfo.PosX:0.00},{e.Info.PosInfo.PosY:0.00}), HitCenter=({e.HitCenterX:0.00},{e.HitCenterY:0.00}), CombatAnchor=({e.CombatAnchorX:0.00},{e.CombatAnchorY:0.00}), CombatLineY={e.CombatAnchorY:0.00}, HitRadius={e.HitRadius:0.00}");
                            spawnPacket.Objects.Add(e.Info);
                        }


                        Console.WriteLine($"[SPAWN_SYNC][SERVER_INITIAL] RoomId={RoomId}, RoomType={RoomType}, RecipientSessionId={player.Session?.SessionId ?? 0}, RecipientPlayerId={player.Id}, Count={spawnPacket.Objects.Count}, Objects={string.Join(";", spawnPacket.Objects.Select(FormatSpawnSyncObject))}");
                        player.Session.Send(spawnPacket);

                    }
                }

                else if (type == GameObjectType.Enemy)
                {

                    if (_enemys.ContainsKey(gameObject.Id))
                    {
                        _enemys.Remove(gameObject.Id);
                    }


                    Enemy enemy = gameObject as Enemy;
                    Console.WriteLine($"[ROOM] Enter Enemy. RoomId={RoomId}, RoomType={roomType}, EnemyId={enemy.Id}, ObjectId={enemy.Info.ObjectId}, Name={enemy.Info.Name}");
                    enemy.Room = this;
                    _enemys.Add(gameObject.Id, enemy);

                }

                else if (type == GameObjectType.Projectile)
                {
                    Projectile projectile = gameObject as Projectile;
                    Console.WriteLine($"[ROOM] Enter Object. RoomId={RoomId}, RoomType={roomType}, ObjectType={type}, ObjectId={projectile.Info.ObjectId}, Name={projectile.Info.Name}");
                    _Projectiles.Add(gameObject.Id, projectile);
                }

                // 타인한테 정보 전송
                {
                    S_Spawn spawnPacket = new S_Spawn();

                    spawnPacket.Objects.Add(gameObject.Info);

                    foreach (Player p in _players.Values)
                    {
                        if (DebugSpawnBroadcastLog)
                        {
                            Console.WriteLine($"[ROOM] Spawn broadcast. RoomId={RoomId}, SpawnObjectId={gameObject.Id}, SpawnName={gameObject.Info.Name}, TargetPlayerId={p.Id}, TargetPlayerName={p.Info.Name}");
                        }

                        bool differentPlayer = gameObject.Info.Name != p.Info.Name;
                        bool visibleToRecipient = ShouldBroadcastSpawnTo(p, gameObject);
                        bool shouldSendSpawn = differentPlayer && visibleToRecipient;
                        if (ObjectManager.GetObjectTypebyId(gameObject.Id) == GameObjectType.Player)
                        {
                            Console.WriteLine($"[SPAWN_SYNC][SERVER_BROADCAST_DECISION] RoomId={RoomId}, RoomType={RoomType}, RecipientSessionId={p.Session?.SessionId ?? 0}, RecipientPlayerId={p.Id}, Subject={FormatSpawnSyncObject(gameObject.Info)}, DifferentPlayer={differentPlayer}, Visible={visibleToRecipient}, Send={shouldSendSpawn}");
                        }

                        if (shouldSendSpawn)
                            p.Session.Send(spawnPacket);
                    }
                }
            }

            if (releaseTownChannelReservation)
                RoomManager.Instance.ReleaseTownChannelReservation(RoomId);

        }

        public void LeaveRoom(int objectId, bool sendLeaveToSelf = true)
        {
            GameObjectType type = ObjectManager.GetObjectTypebyId(objectId);

            lock (_lock)
            {                
                if (type == GameObjectType.Player)
                {
                    _PartyplayerList.Remove(objectId);
                    Player player = null;
                    if (_players.TryGetValue(objectId, out player) == false)
                    {
                        Console.WriteLine($"[ROOM] Leave Player skipped. Reason=PlayerNotFound, RoomId={RoomId}, ObjectId={objectId}");
                        return;
                    }
                    _players.Remove(objectId);
                    player.Room = null;

                    if (sendLeaveToSelf)
                    {
                        S_LeaveGame leavePacket = new S_LeaveGame();
                        player.Session.Send(leavePacket);
                    }

                    // 타인한테 정보 전송
                    {
                        S_Despawn despawnPacket = new S_Despawn();
                        despawnPacket.PlayerIds.Add(player.Id);
                        foreach (Player p in _players.Values)
                        {
                            if (player != p && ShouldBroadcastDespawnTo(p, player))
                                p.Session.Send(despawnPacket);

                            UntrackVisibleLocked(p.Id, player.Id);
                        }

                        _visiblePlayersByObserver.Remove(player.Id);
                    }

                    if (IsDungeonRoom && _players.Count == 0)
                    {
                        RoomManager.Instance.TryRemoveEmptyDungeonRoom(this, "LastPlayerLeft", allowPendingTransfer: false);
                    }
                }

                if(type == GameObjectType.Enemy)
                {
                    Enemy enemy = null;
                    if(_enemys.TryGetValue(objectId, out enemy) == false)
                    {
                        Console.WriteLine($"[ROOM] Leave Enemy skipped. Reason=EnemyNotFound, RoomId={RoomId}, ObjectId={objectId}");
                        return;
                    }

                    _enemys.Remove(objectId);
                    enemy.Room = null;
                   
                    // 타인한테 정보 전송
                    {
                        S_Despawn despawnPacket = new S_Despawn();
                        despawnPacket.PlayerIds.Add(enemy.Id);                      
                        Broadcast(despawnPacket);
                    }
                }

            }
        }

        #endregion EnterRoom

        private string GetRoomTypeForLog()
        {
            return RoomType.ToString();
        }

        #region EnterParty
        public void EnterParty(GameObject gameObject)
        {
            GameObjectType type = ObjectManager.GetObjectTypebyId(gameObject.Id);

            lock (_lock)
            {

                if (gameObject == null)
                    return;

                if (type == GameObjectType.Player)
                {
                    Player player = gameObject as Player;

                    _players.Add(gameObject.Id, player);
                    player.Room = this;

                    LobbyPlayerInfo playerInfo = new LobbyPlayerInfo()
                    {
                        Name = player.Info.Name
                    };


                    _PartyplayerList.Add(gameObject.Id,playerInfo);


                    // 본인한테 정보 전송
                    {
                        S_EnterParty enterPacket = new S_EnterParty();
                        enterPacket.Playerinfo = player.Info;
                        enterPacket.ResponseCode = player.Info.IsMaster == true ? 1 : 2;
                        foreach (LobbyPlayerInfo Lp in _PartyplayerList.Values)
                        {
                            enterPacket.PartyMembers.Add(Lp);
                        }
                        player.Session.Send(enterPacket);

                        foreach (Player p in _players.Values)
                        {
                            if (gameObject.Id != p.Id)
                                p.Session.Send(enterPacket);
                        }
                    }
                }

                else if (type == GameObjectType.Enemy)
                {
                    Enemy enemy = gameObject as Enemy;
                    _enemys.Add(enemy.Id, enemy);
                }
            }
        }

        public void LeaveParty(int objectId)
        {

            Player player = null;
            if (_players.TryGetValue(objectId, out player) == false)
                return;

            _players.Remove(objectId);
            player.Room = null;

            // 본인한테 정보 전송
            {
                S_LeaveGame leavePacket = new S_LeaveGame();
                player.Session.Send(leavePacket);
            }

            // 타인한테 정보 전송
            {
                S_Despawn despawnPacket = new S_Despawn();
                despawnPacket.PlayerIds.Add(player.Info.ObjectId);
                foreach (Player p in _players.Values)
                {
                    if (player != p)
                        p.Session.Send(despawnPacket);
                }
            }

        }

        #endregion EnterParty

        public void OnLeaveGame()
        {
            // TODO
            // DB 연동?
            // -- 피가 깎일 때마다 DB 접근할 필요가 있을까?
            // 1) 서버 다운되면 아직 저장되지 않은 정보 날아감
            // 2) 코드 흐름을 다 막아버린다 !!!!
            // - 비동기(Async) 방법 사용?
            // - 다른 쓰레드로 DB 일감을 던져버리면 되지 않을까?
            // -- 결과를 받아서 이어서 처리를 해야 하는 경우가 많음.
            // -- 아이템 생성

            //DbTransaction.SavePlayerStatus_Step1(this, Room);
        }
        private bool IsTownAoiEnabled()
        {
            return RoomType == Google.Protobuf.Protocol.RoomType.Town;
        }

        private void BroadcastMoveWithAoi(Player mover, S_Move movePacket)
        {
            if (mover == null || movePacket == null)
                return;

            if (IsTownAoiEnabled() == false)
            {
                Broadcast(movePacket);
                return;
            }

            lock (_lock)
            {
                foreach (Player observer in _players.Values)
                {
                    if (observer == null || observer == mover)
                        continue;

                    UpdateTownVisibilityLocked(observer, mover, sendMoveWhenVisible: true, movePacket: movePacket);
                    UpdateTownVisibilityLocked(mover, observer, sendMoveWhenVisible: false, movePacket: null);
                }
            }
        }

        private void UpdateTownVisibilityLocked(Player observer, Player subject, bool sendMoveWhenVisible, S_Move movePacket)
        {
            if (observer == null || subject == null || observer == subject)
                return;

            bool wasVisible = IsVisibleToLocked(observer.Id, subject.Id);
            float radius = wasVisible ? TownAoiExitRadius : TownAoiEnterRadius;
            bool shouldBeVisible = IsWithinTownAoi(observer, subject, radius);

            if (shouldBeVisible)
            {
                if (wasVisible == false)
                {
                    TrackVisibleLocked(observer.Id, subject.Id);
                    S_Spawn spawnPacket = new S_Spawn();
                    spawnPacket.Objects.Add(subject.Info);
                    observer.Session.Send(spawnPacket);
                }

                if (sendMoveWhenVisible)
                    observer.Session.Send(movePacket);

                return;
            }

            if (wasVisible)
            {
                UntrackVisibleLocked(observer.Id, subject.Id);
                S_Despawn despawnPacket = new S_Despawn();
                despawnPacket.PlayerIds.Add(subject.Id);
                observer.Session.Send(despawnPacket);
            }
        }

        private bool ShouldSendInitialSpawn(Player observer, Player subject)
        {
            if (IsTownAoiEnabled() == false)
                return true;

            return IsVisibleToLocked(observer?.Id ?? 0, subject?.Id ?? 0);
        }

        private bool ShouldBroadcastSpawnTo(Player observer, GameObject subject)
        {
            if (IsTownAoiEnabled() == false)
                return true;

            if (observer == null || subject == null)
                return false;

            if (ObjectManager.GetObjectTypebyId(subject.Id) != GameObjectType.Player)
                return true;

            return IsVisibleToLocked(observer.Id, subject.Id);
        }

        private bool ShouldBroadcastDespawnTo(Player observer, Player subject)
        {
            if (IsTownAoiEnabled() == false)
                return true;

            return IsVisibleToLocked(observer?.Id ?? 0, subject?.Id ?? 0);
        }

        private bool IsVisibleToLocked(int observerId, int subjectId)
        {
            HashSet<int> visibleSet = null;
            return _visiblePlayersByObserver.TryGetValue(observerId, out visibleSet) && visibleSet.Contains(subjectId);
        }

        private void TrackVisibleLocked(int observerId, int subjectId)
        {
            if (observerId == subjectId)
                return;

            HashSet<int> visibleSet = null;
            if (_visiblePlayersByObserver.TryGetValue(observerId, out visibleSet) == false)
            {
                visibleSet = new HashSet<int>();
                _visiblePlayersByObserver[observerId] = visibleSet;
            }

            visibleSet.Add(subjectId);
        }

        private void UntrackVisibleLocked(int observerId, int subjectId)
        {
            HashSet<int> visibleSet = null;
            if (_visiblePlayersByObserver.TryGetValue(observerId, out visibleSet))
                visibleSet.Remove(subjectId);
        }

        private bool IsWithinTownAoi(Player a, Player b, float radius)
        {
            if (a?.Info?.PosInfo == null || b?.Info?.PosInfo == null)
                return true;

            // MyRoom is a private per-player space even though its players share
            // the same Town GameRoom. Private players must never enter public AOI.
            if (a.IsInPublicTownArea == false || b.IsInPublicTownArea == false)
                return false;

            float dx = a.Info.PosInfo.PosX - b.Info.PosInfo.PosX;
            float dy = a.Info.PosInfo.PosY - b.Info.PosInfo.PosY;
            return (dx * dx) + (dy * dy) <= radius * radius;
        }



        // Game Room 내부에 패킷을 보낼때 사용
        private static string FormatSpawnSyncObject(ObjectInfo info)
        {
            if (info == null)
                return "null";

            PositionInfo pos = info.PosInfo;
            return $"Id={info.ObjectId},Name={info.Name},Pos=({pos?.PosX ?? 0f:0.00},{pos?.PosY ?? 0f:0.00}),State={pos?.State},Dir={pos?.MoveDir}";
        }

        public void Broadcast(IMessage message)
        {

            foreach (Player player in _players.Values)
            {
                player.Session.Send(message);
            }

        }

    }
}


















