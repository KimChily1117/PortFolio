using System.Collections.Generic;

namespace Server.Game.Room
{
    public sealed class RoomManager
    {
        public static RoomManager Instance { get; } = new RoomManager();

        private readonly Dictionary<int, GameRoom> _rooms = new Dictionary<int, GameRoom>();
        private int _roomId = 1;

        public GameRoom CreateRoom()
        {
            GameRoom room = new GameRoom();
            room.RoomId = _roomId++;
            _rooms.Add(room.RoomId, room);
            return room;
        }

        public void Tick()
        {
            foreach (GameRoom room in _rooms.Values)
                room.Tick();
        }
    }
}