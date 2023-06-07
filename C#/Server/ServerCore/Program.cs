using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ServerCore
{
    class Program
    {
        static Listener _listener = new Listener();
        static Session _session = new Session();

        static void OnCompleteAccess(Socket clientSocket)
        {
            try
            {
                // 서버의 입장에서 손님(Client)을 입장시킴

                //Socket clientSocket = _listener.Accept();

                // 클라이언트에게 수신함

                //byte[] recvBuff = new byte[1024];
                //int recvBytes = clientSocket.Receive(recvBuff);

                //string recvData = Encoding.UTF8.GetString(recvBuff, 0, recvBytes);

                //Console.WriteLine($"From Client : {recvData}");

                ////클라에게 보냄

                _session.Start(clientSocket);

                byte[] sendBuff = Encoding.UTF8.GetBytes("Welcome RPG Server");
                _session.Send(sendBuff);

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

        }



        static void Main(string[] args)
        {
            string host = Dns.GetHostName();
            IPHostEntry iPHost = Dns.GetHostEntry(host);
            IPAddress ipAddr = iPHost.AddressList[0];
            IPEndPoint endPoint = new IPEndPoint(ipAddr, 7777);


            // 문지기의 역활을 함.(Server PPT 참고)

            _listener.Init(endPoint, OnCompleteAccess);
            Console.WriteLine("Listening .....");

            while (true)
            {

            }

        }
    }
}
