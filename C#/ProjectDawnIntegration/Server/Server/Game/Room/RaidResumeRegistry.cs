using System.Collections.Generic;
using System.Linq;
namespace Server.Game.Room
{
    public static class RaidResumeRegistry
    {
        private static readonly object Gate=new object();
        private static readonly Dictionary<int,GameRoom> Rooms=new Dictionary<int,GameRoom>();
        public static bool RegisterParty(IEnumerable<int> characters,GameRoom room){
            var ids=characters.Where(i=>i>0).ToArray();lock(Gate){
                if(ids.Distinct().Count()!=ids.Length||ids.Any(Rooms.ContainsKey))return false;
                foreach(int id in ids)Rooms.Add(id,room);return true;
            }
        }
        public static GameRoom Find(int character){lock(Gate)return Rooms.TryGetValue(character,out var room)?room:null;}
        public static void Remove(int character,GameRoom room){lock(Gate)if(Rooms.TryGetValue(character,out var current)&&ReferenceEquals(current,room))Rooms.Remove(character);}
    }
}
