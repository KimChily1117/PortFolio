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
        //IPEndPoint endPoint = new IPEndPoint(ipAddr, 8080); //기존 테스트 (offline)        
        //ipAddr = IPAddress.Parse("3.24.16.107"); //aws 
        ipAddr = IPAddress.Parse("192.168.0.3"); //aws 

        IPEndPoint endPoint = new IPEndPoint(ipAddr,8080); //포트포워딩 


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
