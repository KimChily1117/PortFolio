using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ServerCore
{
    class Program
    {
        static void Main(string[] args)
        {
            string host = Dns.GetHostName();
            IPHostEntry iPHost = Dns.GetHostEntry(host);
            IPAddress ipAddr = iPHost.AddressList[0];
            IPEndPoint endPoint = new IPEndPoint(ipAddr, 7777);


            // 문지기의 역활을 함.(Server PPT 참고)
            Socket listenSocket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);


            try
            {
                listenSocket.Bind(endPoint);

                listenSocket.Listen(10);


                while (true)
                {
                    Console.WriteLine("Listening .....");

                    // 서버의 입장에서 손님(Client)을 입장시킴

                    Socket clientSocket = listenSocket.Accept();

                    // 클라이언트에게 수신함

                    byte[] recvBuff = new byte[1024];
                    int recvBytes = clientSocket.Receive(recvBuff);

                    string recvData = Encoding.UTF8.GetString(recvBuff, 0, recvBytes);

                    Console.WriteLine($"From Client : {recvData}");

                    //클라에게 보냄

                    byte[] sendBuff = Encoding.UTF8.GetBytes("Welcome RPG Server");
                    clientSocket.Send(sendBuff);
                    
                    //메세지 전송을 완료한뒤 쫓아낸다(Kickout)

                    clientSocket.Shutdown(SocketShutdown.Both);
                    clientSocket.Close();

                }

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

        }
    }
}
