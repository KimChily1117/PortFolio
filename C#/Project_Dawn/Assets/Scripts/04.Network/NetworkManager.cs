using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ServerCore;
using System.Net;
using Google.Protobuf;
using Google.Protobuf.Protocol;

public class NetworkManager
{
    ServerSession _session = new ServerSession();



    public NetworkManager()
    {
        Init();
    }

    public void Send(IMessage packet)
    {
        _session.Send(packet);
    }
    public void Init()
    {
        Connector connector = new Connector();

        string host = Dns.GetHostName();
        IPHostEntry ipHost = Dns.GetHostEntry(host);
        IPAddress ipAddr = ipHost.AddressList[0];
        IPEndPoint endPoint = new IPEndPoint(ipAddr, 7777);


        connector.Connect(endPoint, () =>
        {
            return _session;
        });

    }

    // Update is called once per frame
    public void OnUpdate()
    {
        List<PacketMessage> list = PacketQueue.Instance.PopAll();

        foreach (PacketMessage packet in list)
        {
            Action<PacketSession, IMessage> handler = PacketManager.Instance.GetPacketHandler(packet.Id);

            if (handler != null)
            {
                handler.Invoke(_session, packet.Message);
            }

        }
    }
}
