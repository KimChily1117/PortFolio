using Google.Protobuf;
using Google.Protobuf.Protocol;
using ServerCore;
using System;
using System.Collections.Generic;

namespace DummyClient
{
    public sealed class DummyPacketManager
    {
        public static DummyPacketManager Instance { get; } = new DummyPacketManager();

        private readonly Dictionary<ushort, Action<DummyClientSession, ArraySegment<byte>>> _onRecv = new Dictionary<ushort, Action<DummyClientSession, ArraySegment<byte>>>();
        private readonly Dictionary<ushort, Action<DummyClientSession, IMessage>> _handlers = new Dictionary<ushort, Action<DummyClientSession, IMessage>>();

        private DummyPacketManager()
        {
            Register();
        }

        public void OnRecvPacket(DummyClientSession session, ArraySegment<byte> buffer)
        {
            ushort id = BitConverter.ToUInt16(buffer.Array, buffer.Offset + 2);
            if (_onRecv.TryGetValue(id, out Action<DummyClientSession, ArraySegment<byte>> action))
            {
                action(session, buffer);
                return;
            }

            session.Client.LogVerbose($"Ignored packet id={id}");
        }

        private void Register()
        {
            Register<S_Connected>(MsgId.SConnected, DummyPacketHandler.HandleSConnected);
            Register<S_Login>(MsgId.SLogin, DummyPacketHandler.HandleSLogin);
            Register<S_CreatePlayer>(MsgId.SCreatePlayer, DummyPacketHandler.HandleSCreatePlayer);
            Register<S_EnterGame>(MsgId.SEnterGame, DummyPacketHandler.HandleSEnterGame);
            Register<S_CreateRoom>(MsgId.SCreateRoom, DummyPacketHandler.HandleSCreateRoom);
            Register<S_EnterParty>(MsgId.SEnterParty, DummyPacketHandler.HandleSEnterParty);
            Register<S_SceneMove>(MsgId.SSceneMove, DummyPacketHandler.HandleSSceneMove);
            Register<S_Spawn>(MsgId.SSpawn, DummyPacketHandler.HandleSSpawn);
            Register<S_AddItem>(MsgId.SAddItem, DummyPacketHandler.HandleSAddItem);
            Register<S_Die>(MsgId.SDie, DummyPacketHandler.HandleSDie);
            Register<S_Despawn>(MsgId.SDespawn, DummyPacketHandler.HandleSDespawn);

            Register<S_Move>(MsgId.SMove, DummyPacketHandler.HandleSMove);
            RegisterIgnore<S_Skill>(MsgId.SSkill);
            RegisterIgnore<S_ItemList>(MsgId.SItemList);
            RegisterIgnore<S_EquipItem>(MsgId.SEquipItem);
            RegisterIgnore<S_UdpHello>(MsgId.SUdpHello);
            RegisterIgnore<S_Jump>(MsgId.SJump);
            RegisterIgnore<S_Collision>(MsgId.SCollision);
            RegisterIgnore<S_DungeonClear>(MsgId.SDungeonClear);
        }

        private void Register<T>(MsgId msgId, Action<DummyClientSession, T> handler) where T : IMessage<T>, new()
        {
            ushort id = (ushort)msgId;
            _onRecv[id] = (session, buffer) =>
            {
                T packet = new T();
                packet.MergeFrom(buffer.Array, buffer.Offset + 4, buffer.Count - 4);
                handler(session, packet);
            };
        }

        private void RegisterIgnore<T>(MsgId msgId) where T : IMessage<T>, new()
        {
            ushort id = (ushort)msgId;
            _onRecv[id] = (session, buffer) => session.Client.LogVerbose($"Ignored {msgId}");
        }
    }
}

