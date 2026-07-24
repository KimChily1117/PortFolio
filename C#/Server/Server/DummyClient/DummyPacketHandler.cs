using Google.Protobuf.Protocol;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace DummyClient
{
    public static class DummyPacketHandler
    {
        public static void HandleSConnected(DummyClientSession session, S_Connected packet)
        {
            DummyClient client = session.Client;
            client.MarkConnected();
            client.Session.Send(new C_Login { UniqueId = client.Name });
            client.SetState(DummyClientState.LoginSent);
            client.Log($"C_Login sent. UniqueId={client.Name}");
        }

        public static void HandleSLogin(DummyClientSession session, S_Login packet)
        {
            DummyClient client = session.Client;
            client.MarkLoggedIn(packet.LoginOK == 1);
            client.Log($"S_Login received. LoginOK={packet.LoginOK}, Players={packet.Players.Count}");

            if (packet.LoginOK != 1)
            {
                client.Fail("Login failed");
                return;
            }

            LobbyPlayerInfo existingPlayer = packet.Players.FirstOrDefault(p => p.Name == client.Name);
            if (existingPlayer != null)
            {
                client.MarkPlayerReady();
                SendEnterGame(client);
                return;
            }

            client.Session.Send(new C_CreatePlayer { Name = client.Name });
            client.SetState(DummyClientState.CreatePlayerSent);
            client.Log($"C_CreatePlayer sent. Name={client.Name}");
        }

        public static void HandleSCreatePlayer(DummyClientSession session, S_CreatePlayer packet)
        {
            DummyClient client = session.Client;
            string createdName = packet.Player?.Name;
            if (string.IsNullOrWhiteSpace(createdName))
                createdName = client.Name;

            client.MarkPlayerReady();
            client.Log($"S_CreatePlayer received. Player={createdName}");
            SendEnterGame(client);
        }

        public static void HandleSEnterGame(DummyClientSession session, S_EnterGame packet)
        {
            DummyClient client = session.Client;
            if (packet.Player != null)
                client.PlayerInfo = packet.Player;

            if (client.HasSceneReadySent)
            {
                client.MarkEnteredDungeon();
                client.Log($"Dungeon S_EnterGame received. ObjectId={packet.Player?.ObjectId ?? 0}");
                client.BeginHoldIfReady();
                return;
            }

            if (client.HasEnteredTown == false)
            {
                client.MarkEnteredTown();
                client.Log($"Town S_EnterGame received. ObjectId={packet.Player?.ObjectId ?? 0}");

                if (client.Options.IsMatchScenario)
                    client.SendCreateRoom();
                else if (client.Options.IsTownLoad)
                    client.BeginTownLoadIfReady();
                else if (client.Options.IsCreatePlayers)
                    client.CompleteCreatePlayers();
            }
        }

        public static void HandleSCreateRoom(DummyClientSession session, S_CreateRoom packet)
        {
            DummyClient client = session.Client;
            client.MarkWaitingForExternal();
            client.Log($"S_CreateRoom received. ResponseCode={packet.ResponseCode}");
        }

        public static void HandleSEnterParty(DummyClientSession session, S_EnterParty packet)
        {
            DummyClient client = session.Client;
            client.MarkPartyMatched();
            client.DummyMembers.Clear();
            client.ExternalMembers.Clear();

            foreach (LobbyPlayerInfo member in packet.PartyMembers)
            {
                if (string.IsNullOrWhiteSpace(member.Name))
                    continue;

                if (member.Name.StartsWith(client.Options.Prefix, StringComparison.Ordinal))
                    client.DummyMembers.Add(member.Name);
                else
                    client.ExternalMembers.Add(member.Name);
            }

            client.Log($"S_EnterParty received. Dummy={string.Join(",", client.DummyMembers)}, External={string.Join(",", client.ExternalMembers)}");
        }

        public static void HandleSSceneMove(DummyClientSession session, S_SceneMove packet)
        {
            DummyClient client = session.Client;
            client.TargetRoomId = packet.TargetRoomId;
            client.TransferId = packet.TransferId;
            client.TargetRoomType = packet.TargetRoomType;
            client.SceneType = packet.SceneType;
            client.MarkSceneMoveReceived();
            client.Log($"[DUMMY][SCENE_MOVE] Name={client.Name}, TargetRoomId={packet.TargetRoomId}, TransferId={packet.TransferId}, RoomType={packet.TargetRoomType}, SceneType={packet.SceneType}");
            client.LogVerbose($"[DUMMY][ROOM_TRACK] Name={client.Name}, RoomId={packet.TargetRoomId}, TransferId={packet.TransferId}");

            Task.Run(async () =>
            {
                await Task.Delay(client.Options.SceneReadyDelayMs);
                client.SendSceneReady();
            });
        }

        public static void HandleSSpawn(DummyClientSession session, S_Spawn packet)
        {
            DummyClient client = session.Client;
            client.LogVerbose($"S_Spawn received. Objects={packet.Objects.Count}, Players={packet.Players.Count}");
            client.TrackSpawnObjects(packet.Objects);

            if (client.HasSceneReadySent && client.HasEnteredDungeon == false)
            {
                client.MarkEnteredDungeon();
                client.BeginHoldIfReady();
            }
        }


        public static void HandleSMove(DummyClientSession session, S_Move packet)
        {
            session.Client.ObserveMove(packet);
        }
        public static void HandleSAddItem(DummyClientSession session, S_AddItem packet)
        {
            session.Client.ObserveAddItem(packet);
        }

        public static void HandleSDie(DummyClientSession session, S_Die packet)
        {
            session.Client.MarkObjectDead(packet.Player);
        }

        public static void HandleSDespawn(DummyClientSession session, S_Despawn packet)
        {
            session.Client.RemoveObjects(packet.PlayerIds);
        }

        private static void SendEnterGame(DummyClient client)
        {
            client.Session.Send(new C_EnterGame { Name = client.Name });
            client.SetState(DummyClientState.EnterGameSent);
            client.Log($"C_EnterGame sent. Name={client.Name}");
        }
    }
}




