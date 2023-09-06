using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DummyClient;
using ServerCore;
using System.Net;

public class NetworkManager
{
    ServerSession _session = new ServerSession();
    


    public NetworkManager() 
    {
        Init();
    }    
    
    // Start is called before the first frame update
    public void Init()
    {
        Connector connector = new Connector();


        string host = Dns.GetHostName();
        IPHostEntry ipHost = Dns.GetHostEntry(host);
        IPAddress ipAddr = ipHost.AddressList[0];
        IPEndPoint endPoint = new IPEndPoint(ipAddr, 7777);


        connector.Connect(endPoint,()=>{ 
            return _session; 
        });

    }

    // Update is called once per frame
    public void OnUpdate()
    {
        
    }
}
