using Google.Protobuf.Protocol;
using Server.Game.Object;
using Server.Monitoring;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Server.Game.Room
{
    public class RoomManager
    {
        public static RoomManager Instance { get; } = new RoomManager();

        object _lock = new object();
        readonly object _snapshotLock = new object();
        Dictionary<int, GameRoom> _rooms = new Dictionary<int, GameRoom>();
        Dictionary<int, int> _townChannelReservations = new Dictionary<int, int>();
        List<RoomSnapshot> _cachedRoomSnapshots = new List<RoomSnapshot>();
        DateTime _snapshotUpdatedAtUtc = DateTime.MinValue;
        int _roomId = 1;

        public const int TownChannelMaxPlayers = 50;
        private static readonly TimeSpan EmptyDungeonCleanupGrace = TimeSpan.FromSeconds(30);
        public DateTime SnapshotUpdatedAtUtc
        {
            get
            {
                lock (_snapshotLock)
                {
                    return _snapshotUpdatedAtUtc;
                }
            }
        }

        public GameRoom Add(RoomType roomType)
        {
            GameRoom gameRoom = new GameRoom();
            int totalRooms;

            lock (_lock)
            {
                DateTime nowUtc = DateTime.UtcNow;
                gameRoom.RoomId = _roomId;
                gameRoom.RoomType = roomType;
                gameRoom.CreatedAtUtc = nowUtc;
                gameRoom.LastUpdatedAtUtc = nowUtc;
                gameRoom.State = "Running";

                _rooms.Add(_roomId, gameRoom);
                _roomId++;
                totalRooms = _rooms.Count;
            }

            Console.WriteLine($"[ROOM_MANAGER] Room created. RoomId={gameRoom.RoomId}, RoomType={gameRoom.RoomType}, TotalRooms={totalRooms}");
            RecentEventBuffer.Add(new RecentEventSnapshot
            {
                Type = "RoomCreated",
                RoomId = gameRoom.RoomId,
                RoomType = gameRoom.RoomType.ToString(),
                Count = totalRooms,
                OccurredAtUtc = gameRoom.CreatedAtUtc,
                Detail = $"RoomId={gameRoom.RoomId}, RoomType={gameRoom.RoomType}, TotalRooms={totalRooms}"
            });
            RefreshRoomSnapshotCache(CreateRoomListSnapshot());
            return gameRoom;
        }

        public bool TryRemoveEmptyDungeonRoom(GameRoom room, string reason, bool allowPendingTransfer)
        {
            if (room == null || room.IsDungeonRoom == false || room.IsEmpty == false)
                return false;

            if (allowPendingTransfer == false && SessionManager.Instance.HasPendingTransferToRoom(room.RoomId))
                return false;

            GameRoom removedRoom = null;
            int totalRooms;
            bool removed;

            lock (_lock)
            {
                GameRoom currentRoom = null;
                if (_rooms.TryGetValue(room.RoomId, out currentRoom) == false || ReferenceEquals(currentRoom, room) == false)
                    return false;

                if (currentRoom.IsDungeonRoom == false || currentRoom.IsEmpty == false)
                    return false;

                if (allowPendingTransfer == false && SessionManager.Instance.HasPendingTransferToRoom(currentRoom.RoomId))
                    return false;

                removed = _rooms.Remove(currentRoom.RoomId);
                removedRoom = currentRoom;
                totalRooms = _rooms.Count;
            }

            if (removed)
            {
                removedRoom.State = "Destroyed";
                Console.WriteLine($"[ROOM_MANAGER] Empty dungeon room destroyed. RoomId={removedRoom.RoomId}, RoomType={removedRoom.RoomType}, Reason={reason ?? "Unknown"}, TotalRooms={totalRooms}");
                RecentEventBuffer.Add(new RecentEventSnapshot
                {
                    Type = "RoomRemoved",
                    Reason = reason ?? "Unknown",
                    RoomId = removedRoom.RoomId,
                    RoomType = removedRoom.RoomType.ToString(),
                    Count = totalRooms,
                    OccurredAtUtc = DateTime.UtcNow,
                    Detail = $"RoomId={removedRoom.RoomId}, RoomType={removedRoom.RoomType}, Reason={reason ?? "Unknown"}, TotalRooms={totalRooms}"
                });
                RefreshRoomSnapshotCache(CreateRoomListSnapshot());
            }

            return removed;
        }
        public GameRoom Find(int roomId)
        {
            lock (_lock)
            {
                GameRoom room = null;
                if (_rooms.TryGetValue(roomId, out room))
                    return room;

                return null;
            }
        }

        public GameRoom FindOrCreateTownChannel()
        {
            GameRoom selectedRoom = null;
            int selectedLoad = int.MaxValue;
            int selectedPlayerCount = 0;
            int selectedReservedCount = 0;
            bool created = false;
            int totalRooms = 0;

            lock (_lock)
            {
                foreach (GameRoom room in _rooms.Values)
                {
                    if (room == null || room.RoomType != RoomType.Town || string.Equals(room.State, "Destroyed", StringComparison.OrdinalIgnoreCase))
                        continue;

                    int playerCount = room.PlayerCount;
                    int reservedCount = GetTownReservationCountLocked(room.RoomId);
                    int load = playerCount + reservedCount;
                    if (load >= TownChannelMaxPlayers)
                        continue;

                    if (load < selectedLoad)
                    {
                        selectedRoom = room;
                        selectedLoad = load;
                        selectedPlayerCount = playerCount;
                        selectedReservedCount = reservedCount;
                    }
                }

                if (selectedRoom == null)
                {
                    DateTime nowUtc = DateTime.UtcNow;
                    selectedRoom = new GameRoom
                    {
                        RoomId = _roomId,
                        RoomType = RoomType.Town,
                        CreatedAtUtc = nowUtc,
                        LastUpdatedAtUtc = nowUtc,
                        State = "Running"
                    };
                    _rooms.Add(_roomId, selectedRoom);
                    _roomId++;
                    totalRooms = _rooms.Count;
                    created = true;
                    selectedLoad = 0;
                    selectedPlayerCount = 0;
                    selectedReservedCount = 0;
                }

                ReserveTownChannelLocked(selectedRoom.RoomId);
            }

            if (created)
            {
                Console.WriteLine($"[ROOM_MANAGER] Room created. RoomId={selectedRoom.RoomId}, RoomType={selectedRoom.RoomType}, TotalRooms={totalRooms}");
                RecentEventBuffer.Add(new RecentEventSnapshot
                {
                    Type = "RoomCreated",
                    RoomId = selectedRoom.RoomId,
                    RoomType = selectedRoom.RoomType.ToString(),
                    Count = totalRooms,
                    OccurredAtUtc = selectedRoom.CreatedAtUtc,
                    Detail = $"RoomId={selectedRoom.RoomId}, RoomType={selectedRoom.RoomType}, TotalRooms={totalRooms}"
                });
                RefreshRoomSnapshotCache(CreateRoomListSnapshot());
                Console.WriteLine($"[TOWN_CHANNEL] Created new channel. RoomId={selectedRoom.RoomId}, Reserved=1, MaxPlayers={TownChannelMaxPlayers}");
            }
            else
            {
                Console.WriteLine($"[TOWN_CHANNEL] Assigned existing channel. RoomId={selectedRoom.RoomId}, PlayerCount={selectedPlayerCount}, Reserved={selectedReservedCount + 1}, Load={selectedLoad + 1}, MaxPlayers={TownChannelMaxPlayers}");
            }

            return selectedRoom;
        }

        public bool TryReserveTownChannel(int roomId, out GameRoom room, out string reason)
        {
            room = null;
            reason = null;

            if (roomId <= 0)
            {
                reason = "InvalidTarget";
                return false;
            }

            lock (_lock)
            {
                if (_rooms.TryGetValue(roomId, out room) == false || room == null)
                {
                    reason = "TargetNotFound";
                    return false;
                }

                if (room.RoomType != RoomType.Town || string.Equals(room.State, "Destroyed", StringComparison.OrdinalIgnoreCase))
                {
                    room = null;
                    reason = "TargetNotTown";
                    return false;
                }

                int playerCount = room.PlayerCount;
                int reservedCount = GetTownReservationCountLocked(room.RoomId);
                int load = playerCount + reservedCount;
                if (load >= TownChannelMaxPlayers)
                {
                    room = null;
                    reason = "ChannelFull";
                    return false;
                }

                ReserveTownChannelLocked(room.RoomId);
                Console.WriteLine($"[TOWN_CHANNEL] Reserved target channel. RoomId={room.RoomId}, PlayerCount={playerCount}, Reserved={reservedCount + 1}, Load={load + 1}, MaxPlayers={TownChannelMaxPlayers}");
                return true;
            }
        }
        public void ReleaseTownChannelReservation(int roomId)
        {
            if (roomId <= 0)
                return;

            lock (_lock)
            {
                int count = 0;
                if (_townChannelReservations.TryGetValue(roomId, out count) == false || count <= 0)
                    return;

                count--;
                if (count <= 0)
                    _townChannelReservations.Remove(roomId);
                else
                    _townChannelReservations[roomId] = count;
            }
        }

        private int GetTownReservationCountLocked(int roomId)
        {
            int count = 0;
            _townChannelReservations.TryGetValue(roomId, out count);
            return Math.Max(0, count);
        }

        private void ReserveTownChannelLocked(int roomId)
        {
            int count = GetTownReservationCountLocked(roomId);
            _townChannelReservations[roomId] = count + 1;
        }
        public bool Remove(int roomId)
        {
            GameRoom removedRoom = null;
            int totalRooms;
            bool removed;

            lock (_lock)
            {
                removed = _rooms.TryGetValue(roomId, out removedRoom) && _rooms.Remove(roomId);
                totalRooms = _rooms.Count;
            }

            if (removed)
                Console.WriteLine($"[ROOM_MANAGER] Room removed. RoomId={roomId}, RoomType={removedRoom?.RoomType}, TotalRooms={totalRooms}");
                RecentEventBuffer.Add(new RecentEventSnapshot
                {
                    Type = "RoomRemoved",
                    RoomId = roomId,
                    RoomType = removedRoom?.RoomType.ToString(),
                    Count = totalRooms,
                    OccurredAtUtc = DateTime.UtcNow,
                    Detail = $"RoomId={roomId}, RoomType={removedRoom?.RoomType}, TotalRooms={totalRooms}"
                });

            if (removed)
                RefreshRoomSnapshotCache(CreateRoomListSnapshot());

            return removed;
        }

        public GameRoom Find(RoomType roomType)
        {
            if (roomType != RoomType.Town)
                Console.WriteLine($"[ROOM_MANAGER] Find(RoomType) is legacy and unsafe for multi-room dungeon lookup. RoomType={roomType}. Use Find(roomId) for dungeon rooms.");

            int roomId = (int)roomType + 1;

            lock (_lock)
            {
                GameRoom room = null;
                if (_rooms.TryGetValue(roomId, out room))
                    return room;

                return null;
            }
        }

        public bool Remove(RoomType roomType)
        {
            if (roomType != RoomType.Town)
                Console.WriteLine($"[ROOM_MANAGER] Remove(RoomType) is legacy and unsafe for multi-room dungeon cleanup. RoomType={roomType}. Use Remove(roomId) for dungeon rooms.");

            int roomId = (int)roomType + 1;
            return Remove(roomId);
        }

        public List<RoomSnapshot> GetRoomSnapshots()
        {
            lock (_snapshotLock)
            {
                return _cachedRoomSnapshots
                    .Select(CloneRoomSnapshot)
                    .ToList();
            }
        }

        public RoomSnapshot GetRoomSnapshot(int roomId)
        {
            lock (_snapshotLock)
            {
                RoomSnapshot snapshot = _cachedRoomSnapshots.FirstOrDefault(r => r.RoomId == roomId);
                return snapshot == null ? null : CloneRoomSnapshot(snapshot);
            }
        }

        public void PrintRoomSnapshotSummary()
        {
            List<RoomSnapshot> snapshots = GetRoomSnapshots();
            Console.WriteLine($"[ROOM_SNAPSHOT] Rooms={snapshots.Count}");

            foreach (RoomSnapshot snapshot in snapshots.OrderBy(s => s.RoomId))
            {
                string boss = string.IsNullOrWhiteSpace(snapshot.BossName)
                    ? "None"
                    : $"{snapshot.BossName}, BossHp={snapshot.BossHp ?? 0}/{snapshot.BossMaxHp ?? 0}, BossDead={snapshot.BossIsDead}";

                Console.WriteLine($"[ROOM_SNAPSHOT] RoomId={snapshot.RoomId}, RoomType={snapshot.RoomType}, State={snapshot.State}, Players={snapshot.PlayerCount}, Enemies={snapshot.EnemyCount}, Boss={boss}, UpdateMs={snapshot.LastUpdateMs:0.00}, MaxUpdateMs={snapshot.MaxUpdateMs:0.00}, UpdateCount={snapshot.UpdateCount}");
            }
        }

        public void UpdateRooms()
        {
            List<GameRoom> rooms = CreateRoomListSnapshot();

            foreach (GameRoom room in rooms)
            {
                room?.Update();
            }

            CleanupEmptyDungeonRooms(rooms);
            RefreshRoomSnapshotCache(CreateRoomListSnapshot());
        }

        public void AllLeaveroom(Player MyPlayer)
        {
            List<GameRoom> rooms = CreateRoomListSnapshot();

            foreach (GameRoom room in rooms)
            {
                room?.LeaveRoom(MyPlayer.Info.ObjectId);
            }
        }

        private void CleanupEmptyDungeonRooms(List<GameRoom> rooms)
        {
            if (rooms == null || rooms.Count == 0)
                return;

            DateTime nowUtc = DateTime.UtcNow;
            foreach (GameRoom room in rooms)
            {
                if (room == null || room.IsDungeonRoom == false || room.IsEmpty == false)
                    continue;

                if (nowUtc - room.CreatedAtUtc < EmptyDungeonCleanupGrace)
                    continue;

                TryRemoveEmptyDungeonRoom(room, "PeriodicEmptyDungeonCleanup", allowPendingTransfer: false);
            }
        }
        private List<GameRoom> CreateRoomListSnapshot()
        {
            lock (_lock)
            {
                return new List<GameRoom>(_rooms.Values);
            }
        }

        private void RefreshRoomSnapshotCache(List<GameRoom> rooms)
        {
            List<RoomSnapshot> snapshots = rooms
                .Where(room => room != null)
                .Select(room => room.CreateSnapshot())
                .OrderBy(snapshot => snapshot.RoomId)
                .ToList();

            lock (_snapshotLock)
            {
                _cachedRoomSnapshots = snapshots;
                _snapshotUpdatedAtUtc = DateTime.UtcNow;
            }
        }

        private static RoomSnapshot CloneRoomSnapshot(RoomSnapshot source)
        {
            if (source == null)
                return null;

            return new RoomSnapshot
            {
                RoomId = source.RoomId,
                RoomType = source.RoomType,
                State = source.State,
                CreatedAtUtc = source.CreatedAtUtc,
                LastUpdatedAtUtc = source.LastUpdatedAtUtc,
                ElapsedSeconds = source.ElapsedSeconds,
                PlayerCount = source.PlayerCount,
                EnemyCount = source.EnemyCount,
                IsEmpty = source.IsEmpty,
                IsDungeonRoom = source.IsDungeonRoom,
                Players = source.Players.Select(ClonePlayerSnapshot).ToList(),
                Enemies = source.Enemies.Select(CloneEnemySnapshot).ToList(),
                UpdateCount = source.UpdateCount,
                LastUpdateMs = source.LastUpdateMs,
                MaxUpdateMs = source.MaxUpdateMs,
                BossObjectId = source.BossObjectId,
                BossName = source.BossName,
                BossHp = source.BossHp,
                BossMaxHp = source.BossMaxHp,
                BossIsDead = source.BossIsDead
            };
        }

        private static RoomPlayerSnapshot ClonePlayerSnapshot(RoomPlayerSnapshot source)
        {
            if (source == null)
                return null;

            return new RoomPlayerSnapshot
            {
                ObjectId = source.ObjectId,
                PlayerDbId = source.PlayerDbId,
                Name = source.Name,
                Hp = source.Hp,
                MaxHp = source.MaxHp,
                PosX = source.PosX,
                PosY = source.PosY,
                State = source.State
            };
        }

        private static RoomEnemySnapshot CloneEnemySnapshot(RoomEnemySnapshot source)
        {
            if (source == null)
                return null;

            return new RoomEnemySnapshot
            {
                ObjectId = source.ObjectId,
                TemplateId = source.TemplateId,
                Name = source.Name,
                Hp = source.Hp,
                MaxHp = source.MaxHp,
                PosX = source.PosX,
                PosY = source.PosY,
                IsDead = source.IsDead
            };
        }
    }
}







