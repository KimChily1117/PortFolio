using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Google.Protobuf.Protocol;
using Server;
using Server.Data;
using Server.Game;
using Server.Game.Object;
using Server.Game.Room;

// Test-only executable. Production Server.dll contains no fixture login endpoint.
// Binds loopback explicitly, limits fixture accounts to four, and never opens a DB connection.
internal static class Program
{
    static int Main(string[] args)
    {
        int port=args.Length>0?int.Parse(args[0]):28180;
        int seconds=args.Length>1?int.Parse(args[1]):180;
        Environment.SetEnvironmentVariable("PROJECT_DAWN_MATCH_HISTORY_DISABLED","true");
        DataManager.ItemDict.Add(1,new WeaponData{id=1,itemType=ItemType.Weapon,damage=10});
        DataManager.ItemDict.Add(100,new ArmorData{id=100,itemType=ItemType.Armor,defence=2});
        var town=RoomManager.Instance.Add(RoomType.Town);
        var listener=new TcpListener(IPAddress.Loopback,port);listener.Start(4);
        Console.WriteLine("[UNITY_PLAYTEST_HOST] Loopback="+port+" fixture accounts only; production match/room/packets/core");
        var clock=System.Diagnostics.Stopwatch.StartNew();int count=0;
        try
        {
            while(clock.Elapsed.TotalSeconds<seconds)
            {
                while(listener.Pending())
                {
                    Socket accepted=listener.AcceptSocket();accepted.NoDelay=true;
                    if(count>=4){accepted.Close();continue;}count++;
                    var session=SessionManager.Instance.Generate();session.AccountDbId=1000+count;
                    var player=ObjectManager.Instance.Add<Player>();player.PlayerDbId=1000+count;player.Info.Name="UnityPlayer"+count;
                    player.Session=session;session.MyPlayer=player;
                    player.Inven.Add(new Weapon(1){ItemDbId=1,Equipped=true});player.Inven.Add(new Armor(100){ItemDbId=100,Equipped=true});
                    session.Start(accepted);town.EnterRoom(player);session.OnConnected(accepted.RemoteEndPoint);
                }
                RoomManager.Instance.UpdateRooms();Thread.Sleep(5);
            }
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
        finally {listener.Stop();}
        Console.WriteLine("[UNITY_PLAYTEST_HOST_STOPPED]");return 0;
    }
}
