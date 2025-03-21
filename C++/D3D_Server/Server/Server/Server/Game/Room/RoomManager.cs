﻿using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Server.Game.Room
{
    public class RoomManager
    {
        public static RoomManager Instance { get; } = new RoomManager();
        object _lock = new object();
        Dictionary<int, GameRoom> _rooms = new Dictionary<int, GameRoom>();
        int _roomId = 0;

        public GameRoom Add(int roomId)
        {
            GameRoom gameRoom = new GameRoom();
            _roomId = roomId;
            lock (_lock)
            {
                gameRoom.RoomId = _roomId;
                _rooms.Add(_roomId, gameRoom);
                _roomId++;
            }

            gameRoom.Init();
            return gameRoom;
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

        public bool Remove(int roomId)
        {
            lock (_lock)
            {
                return _rooms.Remove(roomId);
            }
        }

        public void UpdateRooms()
        {
            foreach (GameRoom room in _rooms.Values)
            {
                room.Update();
            }
        }


        //public void AllLeaveroom(Player MyPlayer)
        //{
        //    foreach (GameRoom room in _rooms.Values)
        //    {
        //        room?.LeaveRoom(MyPlayer.Info.ObjectId);
        //    }

        //}
    }
}
