<<<<<<< Updated upstream
﻿using System;
=======
﻿using ServerCore;
using System;
>>>>>>> Stashed changes
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace DummyClient
{
<<<<<<< Updated upstream
    class Program
    {
        static void Main(string[] args)
        {
            string host = Dns.GetHostName();
            IPHostEntry iPHost = Dns.GetHostEntry(host);
            IPAddress ipAddr = iPHost.AddressList[0];
            IPEndPoint endPoint = new IPEndPoint(ipAddr, 7777);


            
            
            //while (true)
            //{
                // 문지기의 역활을 함.(Server PPT 참고)
                Socket socket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                try
                {

                    Console.WriteLine($"Client On");
                    //Server에 연결

                    Thread.Sleep(1);

                    socket.Connect(endPoint);
                    //Server로 송신

                    byte[] sendBuff = Encoding.UTF8.GetBytes("HelloWorld!!");
                    int sendBytes = socket.Send(sendBuff);

                    //결과를 받음

                    byte[] recvBuff = new byte[1024];
                    int recvbytes = socket.Receive(recvBuff); 

                    string recvData = Encoding.UTF8.GetString(recvBuff, 0, recvbytes);
                    Console.WriteLine($"Server -> Client : {recvData}");

                   
                }


                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }


            //    Thread.Sleep(100);


            //}


        }
    }
=======
	class Program
	{
		static void Main(string[] args)
		{
			// DNS (Domain Name System)
			string host = Dns.GetHostName();
			IPHostEntry ipHost = Dns.GetHostEntry(host);
			IPAddress ipAddr = ipHost.AddressList[0];
			IPEndPoint endPoint = new IPEndPoint(ipAddr, 7777);

			Connector connector = new Connector();

			connector.Connect(endPoint, () => { return new ServerSession(); });

			while (true)
			{
				try
				{
				}
				catch (Exception e)
				{
					Console.WriteLine(e.ToString());
				}

				Thread.Sleep(100);
			}
		}
	}
>>>>>>> Stashed changes
}
