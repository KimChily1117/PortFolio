//using Google.Protobuf;
//using Google.Protobuf.Protocol;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Numerics;
//using Server.Game.Objects;
//using Server.Game.Navigation;
//using System.Threading;
//using ServerCore;

//using Vec3 = System.Numerics.Vector3;
//using System.Xml.Linq;
//using System.IO;


//public static class Vector3Extensions
//{
//    // ??System.Numerics.Vector3 ??Google.Protobuf.Protocol.Vector3 변??
//    public static Google.Protobuf.Protocol.Vector3 ToProtoVector3(this System.Numerics.Vector3 vec)
//    {
//        return new Google.Protobuf.Protocol.Vector3 { X = vec.X, Y = vec.Y, Z = vec.Z };
//    }

//    // ??Google.Protobuf.Protocol.Vector3 ??System.Numerics.Vector3 변??
//    public static System.Numerics.Vector3 ToNumericsVector3(this Google.Protobuf.Protocol.Vector3 vec)
//    {
//        return vec != null ? new System.Numerics.Vector3(vec.X, vec.Y, vec.Z) : System.Numerics.Vector3.Zero;
//    }
//}

//namespace Server.Game.Room
//{
//    public class GameRoom
//    {
//        private object _lock = new object();
//        private Dictionary<ulong, Player> _players = new Dictionary<ulong, Player>();
//        private List<Projectile> _projectiles = new List<Projectile>();
//        //private Dictionary<ulong, Monster> _monsters = new Dictionary<ulong, Monster>();

//        public int RoomId { get; set; }
//        public Tilemap _tilemap = new Tilemap();

//        private ulong _projectileIdCounter = 1;

//        public ulong GenerateProjectileId()
//        {
//            return _projectileIdCounter++;
//        }

//        public void Init()
//        {
//            // ?�� ?�?�맵 ?�일 경로 ?�정 (?��? 경로)
//            string tilemapPath = "../../../../Resources/TilemapData.txt";

//            // ???�일 존재 ?��? ?�인
//            if (!File.Exists(tilemapPath))
//            {
//                Console.WriteLine($"[Server] ??Tilemap file not found: {tilemapPath}");
//                return;
//            }

//            // ???�?�맵 로드
//            try
//            {
//                _tilemap.LoadFile(tilemapPath);
//                Console.WriteLine($"[Server] ??Tilemap successfully loaded from {tilemapPath}");
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"[Server] ??Error loading tilemap: {ex.Message}");
//            }

//            //// ??몬스??초기 ?�성 (추후 ?�성??가??
//            //Monster monster = new Monster
//            //{
//            //    Info = new ObjectInfo
//            //    {
//            //        PosX = 8,
//            //        PosY = 8,
//            //        ObjectId = (ulong)new Random().Next(1000, 9999),
//            //        ObjectType = ObjectType.Monster
//            //    }
//            //};
//            //AddObject(monster);
//        }

//        public void Update()
//        {
//            lock (_lock)
//            {
//                foreach (var player in _players.Values)
//                    player.Update();

//                //foreach (var monster in _monsters.Values)
//                //    monster.Update();

//                UpdateProjectiles();

//            }
//        }

//        private void UpdateProjectiles()
//        {
//            List<Projectile> toRemove = new List<Projectile>();

//            lock (_lock)
//            {
//                foreach (var projectile in _projectiles)
//                {
//                    projectile.Update(); // ??Projectile ?�동 ?�데?�트

//                    //// ??충돌 감�?
//                    //GameObject hitObject = DetectCollision(projectile);
//                    //if (hitObject != null)
//                    //{
//                    //    //projectile.OnHit(hitObject); // ??충돌 처리
//                    //    toRemove.Add(projectile);    // ??충돌???�사�??�거 ?�??추�?
//                    //}
//                }

//                //// ??충돌??발생??Projectile ?�거
//                //foreach (var projectile in toRemove)
//                //{
//                //    RemoveProjectile(projectile);
//                //}
//            }
//        }
//        private GameObject DetectCollision(Projectile projectile)
//        {
//            foreach (var obj in GetObjectsInRange(projectile.Position, 0.5f)) // ???��? 반경 ??충돌 감�?
//            {
//                if (obj.Info.TeamId != projectile.CasterTeamId) // ???�군 공격 ?�외
//                {
//                    return obj; // ??충돌???�브?�트 반환
//                }
//            }
//            return null;
//        }


//        public void EnterRoom(ClientSession session)
//        {
//            lock (_lock)
//            {
//                // ???� 배정 (?�제: ?�??ID 블루?�, 짝수 ID ?�드?�)
//                int teamId = (_players.Count % 2 == 0) ? 1 : 2;
//                Random _random = new Random();
//                Vec3 spawnPos = new Vec3();

//                // ???�레?�어 ?�폰 ?�치 ?�정
//                //Vec3 spawnPos = GetRandomEmptyCellPos();
//                if (teamId == 1)
//                {
//                    int randX = _random.Next(6, 21);
//                    int randZ = _random.Next(3, 11);
//                    spawnPos = new Vec3(randX, 2, randZ);
//                }

//                else if(teamId == 2)
//                { 
//                    //int randX = _random.Next(140, 143);
//                    //int randZ = _random.Next(138, 140);
//                    spawnPos = new Vec3(24, 2, 24);
//                }

//                Player player = new Player
//                {
//                    Info = new ObjectInfo
//                    {
//                        ObjectId = (ulong)session.SessionId,
//                        ChampType = PLAYER_CHAMPION_TYPE.PlayerTypeGaren,
//                        State = OBJECT_STATE_TYPE.Idle,
//                        ObjType = OBJECT_TYPE.Player, // 본인?� Player ?�??
//                        Name = $"Client_Yeop_{session.SessionId}",
//                        MaxHp = 1000,
//                        Hp = 1000,
//                        Attack = 70,
//                        Defence = 25,
//                        Position = spawnPos.ToProtoVector3(),
//                        TeamId = teamId
//                    },
//                    Session = session
//                };

//                session.GameRoom = this;
//                session.Player = player;
//                AddObject(player);

//                // ??1. ?�기 ?�신?�게 S_MyPlayer ?�킷 ?�송
//                S_MyPlayer myPlayerPacket = new S_MyPlayer { Info = player.Info };
//                session.Send(myPlayerPacket);

//                // ??2. ?�재 방에 ?�는 기존 ?�레?�어???�보�??�로 ?�속???�레?�어?�게 ?�송
//                S_AddObject existingPlayersPacket = new S_AddObject();
//                foreach (var p in _players.Values)
//                {
//                    if (p.Info.ObjectId != player.Info.ObjectId) // 본인?� ?�외
//                        existingPlayersPacket.Objects.Add(p.Info);
//                }
//                session.Send(existingPlayersPacket);

//                // ??3. 기존 ?�레?�어?�에�??�로???�레?�어???�보�??�송
//                S_AddObject newPlayerPacket = new S_AddObject();
//                newPlayerPacket.Objects.Add(player.Info);
//                Broadcast(newPlayerPacket, player.Info.ObjectId); // ?�기 ?�신?�게???�송 ????

//                // ??4. ?�기 ?�신?�게??S_AddObject�??�송?�여 ?�상?�으�?보이�???
//                session.Send(newPlayerPacket);

//                // ??5. ?�장???�개(Fog of War) 갱신
//                //UpdateFogOfWar(player);
//            }
//        }

//        public void HandleMove(C_Move packet)
//        {
//            lock (_lock)
//            {
//                ulong id = packet.ObjectId;
//                GameObject obj = FindObject(id);
//                if (obj == null)
//                    return;

//                Vec3 targetPos = new Vec3(packet.TargetPos.X, 0, packet.TargetPos.Y);

//                // ???�동 가???��? 체크
//                if (!CanGo(targetPos))
//                    return;

//                obj.Info.Position.X = targetPos.X;
//                obj.Info.Position.Y = targetPos.Y;

//                // ???�동 ?�킷 ?�송
//                var movePacket = new S_Move { Info = obj.Info };
//                Broadcast(movePacket);

//                // ???�레?�어 ?�동 ???�개 갱신
//                if (obj is Player player)
//                {
//                    //UpdateFogOfWar(player);
//                }
//            }
//        }

//        public bool CanGo(Vec3 cellPos)
//        {
//            Tile? tile = _tilemap.GetTileAt(cellPos); // ?�?�이 ?�을 ?�도 ?�음 (Nullable 처리)

//            if (tile == null) // ?�� Null 체크 추�?
//            {
//                Console.WriteLine($"[Server] CanGo Failed: No tile at position {cellPos}");
//                return false; // ?�동 불�? 처리
//            }

//            return tile.IsWalkable; // ?�� Nullable???�닌 값으�??�전?�게 반환
//        }


//        //public void UpdateFogOfWar(Player player)
//        //{
//        //    _tilemap.UpdateFogOfWar(new Vec3(player.Info.Position.X, 0, player.Info.Position.Z), 5.0f);
//        //}

//        //public void ApplySkillRange(Vector2Int skillPos, float skillRange)
//        //{
//        //    _tilemap.ApplySkillRange(new Vec3(skillPos.X, 0, skillPos.Y), skillRange);
//        //}

//        public Vec3 GetRandomEmptyCellPos()
//        {
//            Vector2 size = _tilemap._mapSize;
//            // ??y축안?�요? (3차원?�라??
//            while (true)
//            {
//                int x = new Random().Next(0, (int)size.X);
//                int z = new Random().Next(0, (int)size.Y);
//                Vec3 cellPos = new Vec3(x,2,z);
//                if (CanGo(cellPos))
//                    return cellPos;
//            }
//        }

//        public void AddObject(GameObject obj)
//        {
//            lock (_lock)
//            {
//                ulong id = obj.Info.ObjectId;
//                if (_players.ContainsKey(id))
//                    return; // ???��? 존재?�는 경우 추�? X

//                switch (obj.Info.ObjType)
//                {
//                    case OBJECT_TYPE.Player:
//                        _players[id] = (Player)obj;
//                        break;
//                    default:
//                        return;
//                }

//                obj.Room = this;
//            }
//        }



//        public void RemoveObject(ulong id)
//        {
//            lock (_lock)
//            {
//                GameObject obj = FindObject(id);
//                if (obj == null)
//                    return;

//                switch (obj.Info.ObjType)
//                {
//                    case OBJECT_TYPE.Player:
//                        _players.Remove(id);
//                        break;
//                    //case ObjectType.Monster:
//                    //    _monsters.Remove(id);
//                    //    break;
//                    default:
//                        return;
//                }

//                obj.Room = null;

//                // ???�브?�트 ??�� ?�킷 ?�송
//                S_RemoveObject removePacket = new S_RemoveObject();
//                removePacket.Ids.Add(id);
//                Broadcast(removePacket);
//            }
//        }

//        public GameObject FindObject(ulong id)
//        {
//            var go = _players[id];
//            if (go != null)
//                return go;


//            return null;
//        }
//        public void Broadcast(IMessage packet)
//        {
//            lock (_lock)
//            {
//                foreach (var player in _players.Values)
//                    player.Session.Send(packet);
//            }
//        }

//        public void Broadcast(IMessage packet, ulong excludeId = 0)
//        {
//            lock (_lock)
//            {
//                foreach (var player in _players.Values)
//                {
//                    if (player.Info.ObjectId == excludeId) // ???�기 ?�신?� ?�외
//                        continue;
//                    Console.WriteLine($"[Broadcast] Sending to Player ID: {player.Info.ObjectId}");
//                    player.Session.Send(packet);
//                }
//            }
//        }


//        public void UpdatePlayerTilePosition(GameObject player, int prevX, int prevZ, int newX, int newZ)
//        {
//            // ?�� ?�전 ?�?�에???�레?�어 ?�거
//            Tile prevTile = _tilemap.GetTileAt(new Vec3(prevX, 0, prevZ));
//            if (prevTile != null)
//                prevTile.RemovePlayer(player);

//            // ?�� ?�로???�?�에 ?�레?�어 추�?
//            Tile newTile = _tilemap.GetTileAt(new Vec3(newX, 0, newZ));
//            if (newTile != null)
//                newTile.AddPlayer(player);
//        }

//        public List<GameObject> GetObjectsInRange(Vec3 center, float range)
//        {
//            List<GameObject> objectsInRange = new List<GameObject>();

//            foreach (var obj in _players)
//            {
//                float distance = (obj.Value.Info.Position.ToNumericsVector3() - center).Length(); // ???�클리드 거리 계산
//                if (distance <= range)
//                {
//                    objectsInRange.Add(obj.Value);
//                }
//            }

//            return objectsInRange;
//        }

//        public void AddProjectile(Projectile projectile)
//        {
//            lock (_lock)
//            {
//                _projectiles.Add(projectile);
//                projectile.Room = this;
//            }
//        }

//        public void RemoveProjectile(Projectile projectile)
//        {
//            lock (_lock)
//            {
//                _projectiles.Remove(projectile);
//            }
//        }



//    }
//}


using Google.Protobuf;
using Google.Protobuf.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Server.Game.Objects;
using Server.Game.Navigation;
using Server.Game.Movement;
using System.Threading;
using ServerCore;

using Vec3 = System.Numerics.Vector3;
using System.Xml.Linq;
using System.IO;

public static class Vector3Extensions
{
    // ??System.Numerics.Vector3 ??Google.Protobuf.Protocol.Vector3 변??
    public static Google.Protobuf.Protocol.Vector3 ToProtoVector3(this System.Numerics.Vector3 vec)
    {
        return new Google.Protobuf.Protocol.Vector3 { X = vec.X, Y = vec.Y, Z = vec.Z };
    }

    // ??Google.Protobuf.Protocol.Vector3 ??System.Numerics.Vector3 변??
    public static System.Numerics.Vector3 ToNumericsVector3(this Google.Protobuf.Protocol.Vector3 vec)
    {
        return vec != null ? new System.Numerics.Vector3(vec.X, vec.Y, vec.Z) : System.Numerics.Vector3.Zero;
    }
}


namespace Server.Game.Room
{
    public class GameRoom : JobSerializer
    {
        private Dictionary<ulong, Player> _players = new Dictionary<ulong, Player>();
        private List<Projectile> _projectiles = new List<Projectile>();
        private object _lock = new object();
        public Tilemap _tilemap = new Tilemap();

        public GameRoom(NavigationRegistration navigationRegistration)
        {
            if (navigationRegistration == null)
                throw new ArgumentNullException(nameof(navigationRegistration));

            RoomId = navigationRegistration.RoomId;
            SceneId = navigationRegistration.SceneId;
            NavigationMapId = navigationRegistration.NavigationMapId;
            Navigation = navigationRegistration.Asset ?? throw new ArgumentException("Navigation registration has no asset.", nameof(navigationRegistration));
        }

        public int RoomId { get; }
        public int SceneId { get; }
        public string NavigationMapId { get; }
        public string NavigationContentHash => Navigation.ContentHashHex;
        public NavGridAsset Navigation { get; }

        private ulong _projectileIdCounter = 1;
        private ulong _moveIdCounter = 1;
        private ulong _serverTick;

        internal Action<S_MovementSnapshot> MovementSnapshotSink { get; set; }
        internal Action<IMessage> PacketBroadcastSink { get; set; }
        public ulong ServerTick => _serverTick;
        public float LastMovementDeltaTime { get; private set; }
        public MovementDeltaTimeStatus LastMovementDeltaTimeStatus { get; private set; }
        public int LastMovementSnapshotCount { get; private set; }
        public long LastMovementSnapshotPayloadBytes { get; private set; }
        public int LastPathfindingExpandedNodes { get; private set; }
        public double LastPathfindingElapsedMilliseconds { get; private set; }

        public ulong GenerateProjectileId() => _projectileIdCounter++;

        public ulong GenerateMoveId()
        {
            if (_moveIdCounter == ulong.MaxValue)
                throw new InvalidOperationException("ServerMoveId space exhausted for Room " + RoomId + ".");
            return _moveIdCounter++;
        }

        public void Init(string contentRoot)
        {
            if (!NavigationContentPath.TryResolveAssetPath(
                contentRoot,
                "Legacy/TilemapData.txt",
                out string tilemapPath,
                out string pathError))
            {
                throw new InvalidOperationException("Legacy Tilemap path is invalid: " + pathError);
            }

            if (!_tilemap.LoadFile(tilemapPath))
                throw new InvalidOperationException("Required legacy Tilemap failed to load for Room " + RoomId + ".");

            NavigationBoundsValidationResult bounds =
                NavigationBoundsValidator.ValidateLegacyTilemap(Navigation, _tilemap._mapSize);
            if (!bounds.Success)
                throw new InvalidOperationException("Room " + RoomId + " Navigation/Tilemap Bounds mismatch: " + bounds.Message);

            Console.WriteLine("[Navigation] Room " + RoomId + " Bounds verified: " + bounds.Message);
        }

        public void Update()
        {
            Update(0.1f);
        }

        public void Update(float rawDeltaTime)
        {
            LastMovementDeltaTime = ServerMovementSettings.SanitizeDeltaTime(rawDeltaTime, out MovementDeltaTimeStatus deltaStatus);
            LastMovementDeltaTimeStatus = deltaStatus;
            LastMovementSnapshotCount = 0;
            LastMovementSnapshotPayloadBytes = 0;
            LastPathfindingExpandedNodes = 0;
            LastPathfindingElapsedMilliseconds = 0.0;
            _serverTick++;

            if (deltaStatus == MovementDeltaTimeStatus.Clamped)
                Console.WriteLine("[Movement] Delta time clamped. Room=" + RoomId + " Raw=" + rawDeltaTime + " Applied=" + LastMovementDeltaTime);
            else if (deltaStatus == MovementDeltaTimeStatus.Invalid)
                Console.WriteLine("[Movement] Invalid delta time ignored. Room=" + RoomId + " Raw=" + rawDeltaTime);

            Flush();

            foreach (var player in _players.Values)
            {
                PlayerMovementStep step = player.Update(LastMovementDeltaTime);
                if (step.HasSnapshot)
                    BroadcastMovementSnapshot(player, step);
            }

            UpdateProjectiles();

            if (LastPathfindingElapsedMilliseconds >= 10.0)
                Console.WriteLine("[Movement] Slow pathfinding in Room tick. Room=" + RoomId + " Expanded=" + LastPathfindingExpandedNodes + " ElapsedMs=" + LastPathfindingElapsedMilliseconds.ToString("F3"));
        }

        internal void RecordPathfindingMetrics(int expandedNodes, double elapsedMilliseconds)
        {
            if (expandedNodes > 0)
                LastPathfindingExpandedNodes += expandedNodes;
            if (!double.IsNaN(elapsedMilliseconds) && !double.IsInfinity(elapsedMilliseconds) && elapsedMilliseconds > 0.0)
                LastPathfindingElapsedMilliseconds += elapsedMilliseconds;
        }

        public bool TryGatherPlayersForCombatTest(Player requester, out string resultMessage)
        {
            resultMessage = string.Empty;
            if (requester?.Info?.Position == null || !ContainsPlayer(requester))
            {
                resultMessage = "Combat gather rejected: requester is not registered in this Room.";
                return false;
            }
            if (_players.Count < 2)
            {
                resultMessage = "Combat gather waiting: at least two connected players are required.";
                return false;
            }
            if (!Navigation.TryWorldToCell(requester.Info.Position.ToNumericsVector3(), out NavGridCoordinate anchorCell) ||
                !Navigation.IsWalkable(anchorCell.X, anchorCell.Z))
            {
                resultMessage = "Combat gather rejected: requester is outside a Walkable NavGrid Cell.";
                return false;
            }

            List<Player> orderedPlayers = _players.Values
                .OrderBy(player => player.Info.ObjectId)
                .ToList();
            orderedPlayers.Remove(requester);
            orderedPlayers.Insert(0, requester);

            if (!TryFindCombatTestCells(anchorCell, orderedPlayers.Count, out List<NavGridCoordinate> targetCells))
            {
                resultMessage = "Combat gather rejected: not enough nearby Walkable Cells.";
                return false;
            }

            var targetPositions = new List<Vec3>(targetCells.Count);
            for (int index = 0; index < targetCells.Count; ++index)
            {
                if (!Navigation.TryGetCellWorldCenter(targetCells[index].X, targetCells[index].Z, out Vec3 targetPosition))
                {
                    resultMessage = "Combat gather rejected: failed to resolve a target Cell center.";
                    return false;
                }
                float authoritativeY = orderedPlayers[index].Info.Position?.Y ?? Navigation.Origin.Y;
                targetPosition.Y = authoritativeY;
                targetPositions.Add(targetPosition);
            }

            for (int index = 0; index < orderedPlayers.Count; ++index)
            {
                Player player = orderedPlayers[index];
                NavGridCoordinate cell = targetCells[index];
                Vec3 position = targetPositions[index];
                player.CancelMovement();

                int previousTileX = player.TileX;
                int previousTileZ = player.TileZ;
                player.Info.Position = position.ToProtoVector3();
                player.TileX = cell.X;
                player.TileZ = cell.Z;
                UpdatePlayerTilePosition(player, previousTileX, previousTileZ, cell.X, cell.Z);
                player.Info.State = OBJECT_STATE_TYPE.Idle;

                BroadcastCombatTestTeleportSnapshot(player, GenerateMoveId());
            }

            resultMessage = "Combat gather complete: " + orderedPlayers.Count +
                " players placed near Cell " + anchorCell + ".";
            Console.WriteLine("[CombatTest] Room=" + RoomId + " Requester=" + requester.Info.ObjectId +
                " Players=" + orderedPlayers.Count + " Anchor=" + anchorCell);
            return true;
        }

        private bool TryFindCombatTestCells(
            NavGridCoordinate anchorCell,
            int requiredCount,
            out List<NavGridCoordinate> result)
        {
            const int maxVisitedCells = 512;
            result = new List<NavGridCoordinate>(requiredCount);
            if (requiredCount <= 0 || requiredCount > maxVisitedCells)
                return false;

            var queue = new Queue<NavGridCoordinate>();
            var visited = new HashSet<NavGridCoordinate>();
            queue.Enqueue(anchorCell);
            visited.Add(anchorCell);
            var neighborOffsets = new[]
            {
                new NavGridCoordinate(1, 0),
                new NavGridCoordinate(-1, 0),
                new NavGridCoordinate(0, 1),
                new NavGridCoordinate(0, -1),
            };

            while (queue.Count > 0 && visited.Count <= maxVisitedCells)
            {
                NavGridCoordinate current = queue.Dequeue();
                if (Navigation.IsWalkable(current.X, current.Z))
                {
                    result.Add(current);
                    if (result.Count == requiredCount)
                        return true;
                }

                foreach (NavGridCoordinate offset in neighborOffsets)
                {
                    var neighbor = new NavGridCoordinate(current.X + offset.X, current.Z + offset.Z);
                    if (Navigation.IsValidCell(neighbor.X, neighbor.Z) && visited.Add(neighbor))
                        queue.Enqueue(neighbor);
                }
            }

            result.Clear();
            return false;
        }

        private void BroadcastCombatTestTeleportSnapshot(Player player, ulong serverMoveId)
        {
            var snapshot = new S_MovementSnapshot
            {
                ObjectId = player.Info.ObjectId,
                ServerMoveId = serverMoveId,
                ServerTick = _serverTick,
                Position = player.Info.Position.Clone(),
                MovementState = MOVEMENT_SNAPSHOT_STATE.Arrived,
                CurrentWaypointIndex = 0,
                ClientMoveSequence = player.Session?.LastProcessedMoveSequence ?? 0,
                RoomId = RoomId,
            };

            LastMovementSnapshotCount++;
            LastMovementSnapshotPayloadBytes += snapshot.CalculateSize() + 4L;
            if (MovementSnapshotSink != null)
            {
                MovementSnapshotSink(snapshot);
                return;
            }

            foreach (Player roomPlayer in _players.Values)
            {
                if (roomPlayer.Session != null && !roomPlayer.Session.IsDisconnected)
                    roomPlayer.Session.Send(snapshot);
            }
        }
        public void CancelPlayerMovement(Player player)
        {
            if (player == null || !ContainsPlayer(player))
                return;
            PlayerMovementStep step = player.CancelMovement();
            if (step.HasSnapshot)
                BroadcastMovementSnapshot(player, step);
        }

        private void BroadcastMovementSnapshot(Player player, PlayerMovementStep step)
        {
            var snapshot = new S_MovementSnapshot
            {
                ObjectId = player.Info.ObjectId,
                ServerMoveId = step.ServerMoveId,
                ServerTick = _serverTick,
                Position = step.Position.ToProtoVector3(),
                MovementState = step.SnapshotState,
                CurrentWaypointIndex = checked((uint)Math.Max(0, step.CurrentWaypointIndex)),
                ClientMoveSequence = step.ClientMoveSequence,
                RoomId = RoomId,
            };

            LastMovementSnapshotCount++;
            LastMovementSnapshotPayloadBytes += snapshot.CalculateSize() + 4L;
            if (MovementSnapshotSink != null)
            {
                MovementSnapshotSink(snapshot);
                return;
            }

            foreach (var roomPlayer in _players.Values)
            {
                if (roomPlayer.Session != null && !roomPlayer.Session.IsDisconnected)
                    roomPlayer.Session.Send(snapshot);
            }
        }

        private void UpdateProjectiles()
        {
            List<Projectile> toRemove = new List<Projectile>();

            foreach (var projectile in _projectiles)
            {
                projectile.Update(LastMovementDeltaTime);
            }
        }

        public void EnterRoom(ClientSession session)
        {
            Push(() =>
            {
                int teamId = (_players.Count % 2 == 0) ? 1 : 2;
                Random _random = new Random();
                Vec3 spawnPos = teamId == 1 ? new Vec3(_random.Next(6, 21), 2, _random.Next(3, 11)) : new Vec3(_random.Next(120, 124), 2, _random.Next(120,124));


                Player player = new Player
                {
                    Info = new ObjectInfo
                    {
                        ObjectId = (ulong)session.SessionId,
                        ChampType = teamId == 1 ? PLAYER_CHAMPION_TYPE.PlayerTypeGaren : PLAYER_CHAMPION_TYPE.PlayerTypeAnnie,
                        State = OBJECT_STATE_TYPE.Idle,
                        ObjType = OBJECT_TYPE.Player,
                        Name = $"Client_Yeop_{session.SessionId}",
                        MaxHp = 1000,
                        Hp = 1000,
                        Attack = 70,
                        Defence = 25,
                        Position = spawnPos.ToProtoVector3(),
                        TeamId = teamId
                    },
                    Session = session,
                    Room = this
                };

                session.ResetMoveSequence();
                session.GameRoom = this;
                session.Player = player;

                _players[player.Info.ObjectId] = player;
                if (Navigation.TryWorldToCell(spawnPos, out NavGridCoordinate spawnCell))
                {
                    player.TileX = spawnCell.X;
                    player.TileZ = spawnCell.Z;
                    UpdatePlayerTilePosition(player, spawnCell.X, spawnCell.Z, spawnCell.X, spawnCell.Z);
                }

                // 개별 ?�송
                session.Send(new S_MyPlayer { Info = player.Info });
                session.Send(new S_NavigationInfo
                {
                    RoomId = RoomId,
                    FormatVersion = Navigation.FormatVersion,
                    NavigationMapId = NavigationMapId,
                    NavigationContentHash = NavigationContentHash,
                });

                S_AddObject existingPlayersPacket = new S_AddObject();
                foreach (var p in _players.Values)
                {
                    if (p.Info.ObjectId != player.Info.ObjectId)
                        existingPlayersPacket.Objects.Add(p.Info);
                }
                session.Send(existingPlayersPacket);

                S_AddObject newPlayerPacket = new S_AddObject();
                newPlayerPacket.Objects.Add(player.Info);
                Broadcast(newPlayerPacket, player.Info.ObjectId);
            });
        }


        public bool CanGo(Vec3 cellPos)
        {
            Tile? tile = _tilemap.GetTileAt(cellPos);
            if (tile == null)
            {
                Console.WriteLine($"[Server] CanGo Failed: No tile at position {cellPos}");
                return false;
            }
            return tile.IsWalkable;
        }

        public Vec3 GetRandomEmptyCellPos()
        {
            Vector2 size = _tilemap._mapSize;
            while (true)
            {
                int x = new Random().Next(0, (int)size.X);
                int z = new Random().Next(0, (int)size.Y);
                Vec3 cellPos = new Vec3(x, 2, z);
                if (CanGo(cellPos))
                    return cellPos;
            }
        }
        public void AddObject(GameObject obj)
        {
            Push(() =>
            {
                ulong id = obj.Info.ObjectId;
                if (_players.ContainsKey(id))
                    return;

                if (obj.Info.ObjType == OBJECT_TYPE.Player)
                    _players[id] = (Player)obj;

                obj.Room = this;
            });
        }

        public void RemoveObject(ulong id)
        {
            Push(() =>
            {
                if (_players.Remove(id, out Player player))
                {
                    PlayerMovementStep cancellation = player.CancelMovement();
                    if (cancellation.HasSnapshot)
                        BroadcastMovementSnapshot(player, cancellation);
                    player.Room = null;
                    if (player.Session != null && ReferenceEquals(player.Session.Player, player))
                    {
                        player.Session.Player = null;
                        if (ReferenceEquals(player.Session.GameRoom, this))
                            player.Session.GameRoom = null;
                    }

                    S_RemoveObject removePacket = new S_RemoveObject();
                    removePacket.Ids.Add(id);
                    Broadcast(removePacket);
                }
            });
        }

        public bool ContainsPlayer(Player player)
        {
            if (player?.Info == null)
                return false;
            return _players.TryGetValue(player.Info.ObjectId, out Player registered) && ReferenceEquals(registered, player);
        }

        public GameObject FindObject(ulong id)
        {
            _players.TryGetValue(id, out var go);
            return go;
        }

        public void Broadcast(IMessage packet)
        {
            Push(() =>
            {
                if (PacketBroadcastSink != null)
                {
                    PacketBroadcastSink(packet);
                    return;
                }
                foreach (var player in _players.Values)
                    if (player.Session != null && !player.Session.IsDisconnected)
                        player.Session.Send(packet);
            });
        }

        public void Broadcast(IMessage packet, ulong excludeId)
        {
            Push(() =>
            {
                foreach (var player in _players.Values)
                {
                    if (player.Info.ObjectId == excludeId)
                        continue;
                    player.Session.Send(packet);
                }
            });
        }


        public void UpdatePlayerTilePosition(GameObject player, int prevX, int prevZ, int newX, int newZ)
        {
            Tile prevTile = _tilemap.GetTileAt(new Vec3(prevX, 0, prevZ));
            prevTile?.RemovePlayer(player);

            Tile newTile = _tilemap.GetTileAt(new Vec3(newX, 0, newZ));
            newTile?.AddPlayer(player);
        }

        public List<GameObject> GetObjectsInRange(Vec3 center, float range)
        {
            List<GameObject> objectsInRange = new List<GameObject>();
            foreach (var obj in _players)
            {
                Vec3 position = obj.Value.Info.Position.ToNumericsVector3();
                float dx = position.X - center.X;
                float dz = position.Z - center.Z;
                float distanceSquared = dx * dx + dz * dz;
                if (distanceSquared <= range * range)
                    objectsInRange.Add(obj.Value);
            }
            return objectsInRange;
        }

        public void AddProjectile(Projectile projectile)
        {
            _projectiles.Add(projectile);
            projectile.Room = this;
        }

        public void RemoveProjectile(Projectile projectile)
        {
            _projectiles.Remove(projectile);
        }
    }
}

