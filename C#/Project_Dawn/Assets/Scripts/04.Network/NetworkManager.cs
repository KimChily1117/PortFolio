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

    public ApiHelper apiHelper = new ApiHelper();

    ServerSession _session = new ServerSession();
    
    public void Send(IMessage packet)
    {
        _session.Send(packet);
    }
    public void ConnectToGame()
    {
        Connector connector = new Connector();

        string host = Dns.GetHostName();
        IPHostEntry ipHost = Dns.GetHostEntry(host);
        IPAddress ipAddr = ipHost.AddressList[0];
        ipAddr = IPAddress.Parse("3.24.16.107");
        IPEndPoint endPoint = new IPEndPoint(ipAddr, 8080);
        //IPEndPoint endPoint = new IPEndPoint(ipAddr, 7777);


        connector.Connect(endPoint, () =>
        {
            return _session;
        });

    }

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
