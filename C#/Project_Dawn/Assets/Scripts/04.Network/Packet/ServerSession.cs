using Google.Protobuf;
using Google.Protobuf.Protocol;
using ServerCore;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using UnityEngine;

public class ServerSession : PacketSession
{
    public bool IsConnected { get; private set; }
    public Action<bool> ConnectionStateChanged { get; set; }
    public event Action<string> Disconnected;

    public void Send(IMessage packet)
    {
        string msgName = packet.Descriptor.Name.Replace("_", string.Empty);
        MsgId msgId = (MsgId)Enum.Parse(typeof(MsgId), msgName);
        ushort size = (ushort)packet.CalculateSize();
        byte[] sendBuffer = new byte[size + 4];
        Array.Copy(BitConverter.GetBytes((ushort)(size + 4)), 0, sendBuffer, 0, sizeof(ushort));
        Array.Copy(BitConverter.GetBytes((ushort)msgId), 0, sendBuffer, 2, sizeof(ushort));
        Array.Copy(packet.ToByteArray(), 0, sendBuffer, 4, size);
        Send(new ArraySegment<byte>(sendBuffer));
    }


    public override void OnConnected(EndPoint endPoint)
	{
        IsConnected = true;
        ConnectionStateChanged?.Invoke(true);
        Debug.Log($"OnConnected : {endPoint}");

		PacketManager.Instance.CustomHandler = (s, m, i) =>
		{
            PacketQueue.Instance.Push(i, m);
        };
    }

	public override void OnDisconnected(EndPoint endPoint)
	{
        IsConnected = false;
        ConnectionStateChanged?.Invoke(false);
		Debug.Log($"OnDisconnected : {endPoint}");
        Disconnected?.Invoke(endPoint == null ? "Unknown" : endPoint.ToString());
	}

	public override void OnRecvPacket(ArraySegment<byte> buffer)
	{
		PacketManager.Instance.OnRecvPacket(this, buffer);
	}

	public override void OnSend(int numOfBytes)
	{
		//Console.WriteLine($"Transferred bytes: {numOfBytes}");
	}
}



