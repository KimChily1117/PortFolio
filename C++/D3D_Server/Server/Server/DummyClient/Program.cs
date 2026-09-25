using ServerCore;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
namespace DummyClient
{
    public class Program
    {
        static void Main(string[] args)
        {
            // DNS (Domain Name System)
            string host = Dns.GetHostName();
            IPHostEntry ipHost = Dns.GetHostEntry(host);

            IPAddress ipAddr = IPAddress.Parse("127.0.0.1");
            IPEndPoint endPoint = new IPEndPoint(ipAddr, 8080);

            Connector connector = new Connector();

            connector.Connect(endPoint,
            () => { return SessionManager.Instance.Generate(); },
            8);

            while (true)
            {
                try
                {
                    SessionManager.Instance.SendForEach();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }

                Thread.Sleep(4500);
            }

        }
    }
}
