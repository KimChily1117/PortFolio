using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace DummyClient
{
    class Program
    {
        static void Main(string[] args)
        {
            string host = Dns.GetHostName();
            IPHostEntry iPHost = Dns.GetHostEntry(host);
            IPAddress ipAddr = iPHost.AddressList[0];
            IPEndPoint endPoint = new IPEndPoint(ipAddr, 8080);


            // 문지기의 역활을 함.(Server PPT 참고)
            Socket socket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            

            try
            {

                Console.WriteLine($"Client On");
                //Server에 연결
                socket.Connect(endPoint);

                //Server로 송신

                byte[] sendBuff = Encoding.UTF8.GetBytes("HelloWorld!!");
                int sendBytes = socket.Send(sendBuff);

                //결과를 받음

                byte[] recvBuff = new byte[1024];
                int recvbytes = socket.Receive(recvBuff);

                string recvData = Encoding.UTF8.GetString(recvBuff, 0, recvbytes);
                Console.WriteLine($"Server -> Client : {recvData}");

                socket.Shutdown(SocketShutdown.Both);
                socket.Close();
            }


            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }




        }
    }
}
