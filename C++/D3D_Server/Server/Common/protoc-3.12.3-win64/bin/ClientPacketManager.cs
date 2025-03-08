using Google.Protobuf;
using Google.Protobuf.Protocol;
using ServerCore;
using System;
using System.Collections.Generic;

class PacketManager
{
	#region Singleton
	static PacketManager _instance = new PacketManager();
	public static PacketManager Instance { get { return _instance; } }
	#endregion

	PacketManager()
	{
		Register();
	}

	Dictionary<ushort, Action<PacketSession, ArraySegment<byte>, ushort>> _onRecv = new Dictionary<ushort, Action<PacketSession, ArraySegment<byte>, ushort>>();
	Dictionary<ushort, Action<PacketSession, IMessage>> _handler = new Dictionary<ushort, Action<PacketSession, IMessage>>();
		
	public Action<PacketSession, IMessage, ushort> CustomHandler { get; set; }

	public void Register()
	{		
		_onRecv.Add((ushort)MsgId.STestMsg, MakePacket<S_TestMsg>);
		_handler.Add((ushort)MsgId.STestMsg, PacketHandler.S_TestMsgHandler);		
		_onRecv.Add((ushort)MsgId.SObjectUpdate, MakePacket<S_ObjectUpdate>);
		_handler.Add((ushort)MsgId.SObjectUpdate, PacketHandler.S_ObjectUpdateHandler);		
		_onRecv.Add((ushort)MsgId.SSkillResult, MakePacket<S_SkillResult>);
		_handler.Add((ushort)MsgId.SSkillResult, PacketHandler.S_SkillResultHandler);		
		_onRecv.Add((ushort)MsgId.SUpdateMap, MakePacket<S_UpdateMap>);
		_handler.Add((ushort)MsgId.SUpdateMap, PacketHandler.S_UpdateMapHandler);		
		_onRecv.Add((ushort)MsgId.SChatMessage, MakePacket<S_ChatMessage>);
		_handler.Add((ushort)MsgId.SChatMessage, PacketHandler.S_ChatMessageHandler);		
		_onRecv.Add((ushort)MsgId.SEnterGame, MakePacket<S_EnterGame>);
		_handler.Add((ushort)MsgId.SEnterGame, PacketHandler.S_EnterGameHandler);		
		_onRecv.Add((ushort)MsgId.SMyPlayer, MakePacket<S_MyPlayer>);
		_handler.Add((ushort)MsgId.SMyPlayer, PacketHandler.S_MyPlayerHandler);		
		_onRecv.Add((ushort)MsgId.SAddObject, MakePacket<S_AddObject>);
		_handler.Add((ushort)MsgId.SAddObject, PacketHandler.S_AddObjectHandler);		
		_onRecv.Add((ushort)MsgId.SRemoveObject, MakePacket<S_RemoveObject>);
		_handler.Add((ushort)MsgId.SRemoveObject, PacketHandler.S_RemoveObjectHandler);		
		_onRecv.Add((ushort)MsgId.SMove, MakePacket<S_Move>);
		_handler.Add((ushort)MsgId.SMove, PacketHandler.S_MoveHandler);
	}

	public void OnRecvPacket(PacketSession session, ArraySegment<byte> buffer)
	{
		ushort count = 0;

		ushort size = BitConverter.ToUInt16(buffer.Array, buffer.Offset);
		count += 2;
		ushort id = BitConverter.ToUInt16(buffer.Array, buffer.Offset + count);
		count += 2;

		Action<PacketSession, ArraySegment<byte>, ushort> action = null;
		if (_onRecv.TryGetValue(id, out action))
			action.Invoke(session, buffer, id);
	}

	void MakePacket<T>(PacketSession session, ArraySegment<byte> buffer, ushort id) where T : IMessage, new()
	{
		T pkt = new T();
		pkt.MergeFrom(buffer.Array, buffer.Offset + 4, buffer.Count - 4);

		if (CustomHandler != null)
		{
			CustomHandler.Invoke(session, pkt, id);
		}
		else
		{
			Action<PacketSession, IMessage> action = null;
			if (_handler.TryGetValue(id, out action))
				action.Invoke(session, pkt);
		}
	}

	public Action<PacketSession, IMessage> GetPacketHandler(ushort id)
	{
		Action<PacketSession, IMessage> action = null;
		if (_handler.TryGetValue(id, out action))
			return action;
		return null;
	}
}