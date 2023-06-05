using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ServerCore
{
    class Listener
    {
        Socket listenersocket;


        public void Init(Socket socket, IPEndPoint iPEndPoint , )
        {
            listenersocket = socket;
            
        }
    }
}
