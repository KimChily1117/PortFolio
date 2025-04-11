//using Google.Protobuf;
//using Google.Protobuf.Protocol;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Numerics;
//using Server.Game.Objects;
//using System.Threading;
//using ServerCore;

//using Vec3 = System.Numerics.Vector3;
//using System.Xml.Linq;
//using System.IO;


//public static class Vector3Extensions
//{
//    // ✅ System.Numerics.Vector3 → Google.Protobuf.Protocol.Vector3 변환
//    public static Google.Protobuf.Protocol.Vector3 ToProtoVector3(this System.Numerics.Vector3 vec)
//    {
//        return new Google.Protobuf.Protocol.Vector3 { X = vec.X, Y = vec.Y, Z = vec.Z };
//    }

//    // ✅ Google.Protobuf.Protocol.Vector3 → System.Numerics.Vector3 변환
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
//            // 🔥 타일맵 파일 경로 설정 (상대 경로)
//            string tilemapPath = "../../../../Resources/TilemapData.txt";

//            // ✅ 파일 존재 여부 확인
//            if (!File.Exists(tilemapPath))
//            {
//                Console.WriteLine($"[Server] ❌ Tilemap file not found: {tilemapPath}");
//                return;
//            }

//            // ✅ 타일맵 로드
//            try
//            {
//                _tilemap.LoadFile(tilemapPath);
//                Console.WriteLine($"[Server] ✅ Tilemap successfully loaded from {tilemapPath}");
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"[Server] ❌ Error loading tilemap: {ex.Message}");
//            }

//            //// ✅ 몬스터 초기 생성 (추후 활성화 가능)
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
//                    projectile.Update(); // ✅ Projectile 이동 업데이트

//                    //// ✅ 충돌 감지
//                    //GameObject hitObject = DetectCollision(projectile);
//                    //if (hitObject != null)
//                    //{
//                    //    //projectile.OnHit(hitObject); // ✅ 충돌 처리
//                    //    toRemove.Add(projectile);    // ✅ 충돌한 투사체 제거 대상 추가
//                    //}
//                }

//                //// ✅ 충돌이 발생한 Projectile 제거
//                //foreach (var projectile in toRemove)
//                //{
//                //    RemoveProjectile(projectile);
//                //}
//            }
//        }
//        private GameObject DetectCollision(Projectile projectile)
//        {
//            foreach (var obj in GetObjectsInRange(projectile.Position, 0.5f)) // ✅ 작은 반경 내 충돌 감지
//            {
//                if (obj.Info.TeamId != projectile.CasterTeamId) // ✅ 아군 공격 제외
//                {
//                    return obj; // ✅ 충돌한 오브젝트 반환
//                }
//            }
//            return null;
//        }


//        public void EnterRoom(ClientSession session)
//        {
//            lock (_lock)
//            {
//                // ✅ 팀 배정 (예제: 홀수 ID 블루팀, 짝수 ID 레드팀)
//                int teamId = (_players.Count % 2 == 0) ? 1 : 2;
//                Random _random = new Random();
//                Vec3 spawnPos = new Vec3();

//                // ✅ 플레이어 스폰 위치 선정
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
//                        ObjType = OBJECT_TYPE.Player, // 본인은 Player 타입
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

//                // ✅ 1. 자기 자신에게 S_MyPlayer 패킷 전송
//                S_MyPlayer myPlayerPacket = new S_MyPlayer { Info = player.Info };
//                session.Send(myPlayerPacket);

//                // ✅ 2. 현재 방에 있는 기존 플레이어들 정보를 새로 접속한 플레이어에게 전송
//                S_AddObject existingPlayersPacket = new S_AddObject();
//                foreach (var p in _players.Values)
//                {
//                    if (p.Info.ObjectId != player.Info.ObjectId) // 본인은 제외
//                        existingPlayersPacket.Objects.Add(p.Info);
//                }
//                session.Send(existingPlayersPacket);

//                // ✅ 3. 기존 플레이어들에게 새로운 플레이어의 정보를 전송
//                S_AddObject newPlayerPacket = new S_AddObject();
//                newPlayerPacket.Objects.Add(player.Info);
//                Broadcast(newPlayerPacket, player.Info.ObjectId); // 자기 자신에게는 전송 안 함

//                // ✅ 4. 자기 자신에게도 S_AddObject를 전송하여 정상적으로 보이게 함
//                session.Send(newPlayerPacket);

//                // ✅ 5. 전장의 안개(Fog of War) 갱신
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

//                // ✅ 이동 가능 여부 체크
//                if (!CanGo(targetPos))
//                    return;

//                obj.Info.Position.X = targetPos.X;
//                obj.Info.Position.Y = targetPos.Y;

//                // ✅ 이동 패킷 전송
//                var movePacket = new S_Move { Info = obj.Info };
//                Broadcast(movePacket);

//                // ✅ 플레이어 이동 후 안개 갱신
//                if (obj is Player player)
//                {
//                    //UpdateFogOfWar(player);
//                }
//            }
//        }

//        public bool CanGo(Vec3 cellPos)
//        {
//            Tile? tile = _tilemap.GetTileAt(cellPos); // 타일이 없을 수도 있음 (Nullable 처리)

//            if (tile == null) // 🔥 Null 체크 추가
//            {
//                Console.WriteLine($"[Server] CanGo Failed: No tile at position {cellPos}");
//                return false; // 이동 불가 처리
//            }

//            return tile.IsWalkable; // 🔥 Nullable이 아닌 값으로 안전하게 반환
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
//            // 왜 y축안써요? (3차원이라서)
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
//                    return; // ✅ 이미 존재하는 경우 추가 X

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

//                // ✅ 오브젝트 삭제 패킷 전송
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
//                    if (player.Info.ObjectId == excludeId) // ✅ 자기 자신은 제외
//                        continue;
//                    Console.WriteLine($"[Broadcast] Sending to Player ID: {player.Info.ObjectId}");
//                    player.Session.Send(packet);
//                }
//            }
//        }


//        public void UpdatePlayerTilePosition(GameObject player, int prevX, int prevZ, int newX, int newZ)
//        {
//            // 🔥 이전 타일에서 플레이어 제거
//            Tile prevTile = _tilemap.GetTileAt(new Vec3(prevX, 0, prevZ));
//            if (prevTile != null)
//                prevTile.RemovePlayer(player);

//            // 🔥 새로운 타일에 플레이어 추가
//            Tile newTile = _tilemap.GetTileAt(new Vec3(newX, 0, newZ));
//            if (newTile != null)
//                newTile.AddPlayer(player);
//        }

//        public List<GameObject> GetObjectsInRange(Vec3 center, float range)
//        {
//            List<GameObject> objectsInRange = new List<GameObject>();

//            foreach (var obj in _players)
//            {
//                float distance = (obj.Value.Info.Position.ToNumericsVector3() - center).Length(); // ✅ 유클리드 거리 계산
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
using System.Threading;
using ServerCore;

using Vec3 = System.Numerics.Vector3;
using System.Xml.Linq;
using System.IO;

public static class Vector3Extensions
{
    // ✅ System.Numerics.Vector3 → Google.Protobuf.Protocol.Vector3 변환
    public static Google.Protobuf.Protocol.Vector3 ToProtoVector3(this System.Numerics.Vector3 vec)
    {
        return new Google.Protobuf.Protocol.Vector3 { X = vec.X, Y = vec.Y, Z = vec.Z };
    }

    // ✅ Google.Protobuf.Protocol.Vector3 → System.Numerics.Vector3 변환
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

        public int RoomId { get; set; }
        public Tilemap _tilemap = new Tilemap();

        private ulong _projectileIdCounter = 1;
        public ulong GenerateProjectileId() => _projectileIdCounter++;

        public void Init()
        {
            string tilemapPath = "../../../../Resources/TilemapData.txt";
            if (!File.Exists(tilemapPath))
            {
                Console.WriteLine($"[Server] ❌ Tilemap file not found: {tilemapPath}");
                return;
            }

            try
            {
                _tilemap.LoadFile(tilemapPath);
                Console.WriteLine($"[Server] ✅ Tilemap successfully loaded from {tilemapPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Server] ❌ Error loading tilemap: {ex.Message}");
            }
        }

        public void Update()
        {
            Flush();

            foreach (var player in _players.Values)
                player.Update();

            UpdateProjectiles();
        }

        private void UpdateProjectiles()
        {
            List<Projectile> toRemove = new List<Projectile>();

            foreach (var projectile in _projectiles)
            {
                projectile.Update();
            }
        }

        public void EnterRoom(ClientSession session)
        {
            int teamId = (_players.Count % 2 == 0) ? 1 : 2;
            Random _random = new Random();
            Vec3 spawnPos = teamId == 1 ? new Vec3(_random.Next(6, 21), 2, _random.Next(3, 11)) : new Vec3(124, 2, 124);

            Player player = new Player
            {
                Info = new ObjectInfo
                {
                    ObjectId = (ulong)session.SessionId,
                    ChampType = teamId == 1 ? PLAYER_CHAMPION_TYPE.PlayerTypeGaren : PLAYER_CHAMPION_TYPE.PlayerTypeAnnie ,
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
                Session = session
            };

            session.GameRoom = this;
            session.Player = player;
            AddObject(player);

            S_MyPlayer myPlayerPacket = new S_MyPlayer { Info = player.Info };
            session.Send(myPlayerPacket);

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
            session.Send(newPlayerPacket);
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
            ulong id = obj.Info.ObjectId;
            if (_players.ContainsKey(id))
                return;

            if (obj.Info.ObjType == OBJECT_TYPE.Player)
                _players[id] = (Player)obj;

            obj.Room = this;
        }

        public void RemoveObject(ulong id)
        {
            GameObject obj = FindObject(id);
            if (obj == null)
                return;

            if (obj.Info.ObjType == OBJECT_TYPE.Player)
                _players.Remove(id);

            obj.Room = null;

            S_RemoveObject removePacket = new S_RemoveObject();
            removePacket.Ids.Add(id);
            Broadcast(removePacket);
        }

        public GameObject FindObject(ulong id)
        {
            _players.TryGetValue(id, out var go);
            return go;
        }

        public void Broadcast(IMessage packet)
        {
            foreach (var player in _players.Values)
                player.Session.Send(packet);
        }

        public void Broadcast(IMessage packet, ulong excludeId = 0)
        {
            foreach (var player in _players.Values)
            {
                if (player.Info.ObjectId == excludeId)
                    continue;
                player.Session.Send(packet);
            }
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
                float distance = (obj.Value.Info.Position.ToNumericsVector3() - center).Length();
                if (distance <= range)
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

