using Google.Protobuf;
using Google.Protobuf.Protocol;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Server;
using Server.Data;
using Server.DB;
using ServerCore;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

internal static class Program
{
    private static int Main()
    {
        string oldConfigPath = Environment.GetEnvironmentVariable("PROJECT_DAWN_CONFIG_PATH");
        string oldConnection = Environment.GetEnvironmentVariable("PROJECT_DAWN_DB_CONNECTION_STRING");
        string testDirectory = Path.Combine(Path.GetTempPath(), "project-dawn-regression-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testDirectory);
        try
        {
            TestConfiguration(testDirectory);
            TestFailedLoginKeepsReceiving();
            TestReceiveFailureClosesSocket(false);
            TestReceiveFailureClosesSocket(true);
            TestMalformedPacketClosesSocket();
            Console.WriteLine("PASS: configuration, failed login retry, receive cleanup, disconnect cleanup, packet validation");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL: " + ex);
            return 1;
        }
        finally
        {
            Environment.SetEnvironmentVariable("PROJECT_DAWN_CONFIG_PATH", oldConfigPath);
            Environment.SetEnvironmentVariable("PROJECT_DAWN_DB_CONNECTION_STRING", oldConnection);
        }
    }

    private static void TestConfiguration(string testDirectory)
    {
        string configPath = Path.Combine(testDirectory, "config.json");
        File.WriteAllText(configPath, JsonConvert.SerializeObject(new
        {
            dataPath = "data",
            connectionString = "Data Source=config-db;Initial Catalog=GameDB;Integrated Security=true"
        }));
        Environment.SetEnvironmentVariable("PROJECT_DAWN_CONFIG_PATH", configPath);
        Environment.SetEnvironmentVariable("PROJECT_DAWN_DB_CONNECTION_STRING", null);
        ConfigManager.LoadConfig();
        Assert(ConfigManager.Config.dataPath == Path.Combine(testDirectory, "data"), "dataPath must be relative to config.json");
        using (AppDbContext db = new AppDbContext())
            Assert(db.Database.GetDbConnection().ConnectionString.Contains("config-db"), "config DB address was ignored");

        Environment.SetEnvironmentVariable("PROJECT_DAWN_DB_CONNECTION_STRING", "Data Source=environment-db;Initial Catalog=GameDB;Integrated Security=true");
        using (AppDbContext db = new AppDbContext())
            Assert(db.Database.GetDbConnection().ConnectionString.Contains("environment-db"), "environment override was ignored");
        using (AppDbContext db = new AppDbContext("Data Source=explicit-db;Initial Catalog=GameDB;Integrated Security=true"))
            Assert(db.Database.GetDbConnection().ConnectionString.Contains("explicit-db"), "explicit connection override was ignored");
        Console.WriteLine("PASS: config path and DB connection precedence");
    }

    private static void TestFailedLoginKeepsReceiving()
    {
        Environment.SetEnvironmentVariable("PROJECT_DAWN_DB_CONNECTION_STRING",
            "Data Source=tcp:127.0.0.1,1;Initial Catalog=ProjectDawnRegression;Integrated Security=true;Connect Timeout=1;Pooling=false");
        ClientSession session = new ClientSession { SessionId = int.MaxValue };
        using (SocketPair pair = new SocketPair(session))
        {
            for (int attempt = 0; attempt < 2; attempt++)
            {
                C_Login request = new C_Login { UniqueId = "PD_Regression_LoginFailure" };
                pair.SendPacket((ushort)MsgId.CLogin, request.ToByteArray());
                byte[] response = pair.ReadPacket();
                Assert(BitConverter.ToUInt16(response, 2) == (ushort)MsgId.SLogin, "expected S_Login failure response");
                S_Login login = S_Login.Parser.ParseFrom(response, 4, response.Length - 4);
                Assert(login.LoginOK == 0, "unavailable DB must reject login");
                Assert(session.AccountDbId == 0 && session.UdpToken == null, "failed login retained account or UDP state");
            }
        }
        Console.WriteLine("PASS: DB failure responds immediately and a second login is received");
    }

    private static void TestReceiveFailureClosesSocket(bool throwOnDisconnect)
    {
        FaultSession session = new FaultSession { ThrowOnDisconnect = throwOnDisconnect };
        using (SocketPair pair = new SocketPair(session))
        {
            pair.SendPacket(0, new byte[0]);
            Assert(pair.Client.Receive(new byte[1]) == 0, "receive exception left the TCP connection open");
            Assert(session.Disconnected.Wait(1000), "disconnect callback was skipped");
        }
        Console.WriteLine("PASS: socket closes after handler exception; ThrowOnDisconnect=" + throwOnDisconnect);
    }

    private static void TestMalformedPacketClosesSocket()
    {
        FaultSession session = new FaultSession();
        using (SocketPair pair = new SocketPair(session))
        {
            pair.Client.Send(new byte[] { 0, 0, 0, 0 });
            Assert(pair.Client.Receive(new byte[1]) == 0, "zero-sized packet must close the socket");
        }
        Console.WriteLine("PASS: malformed packet closes the connection");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class FaultSession : PacketSession
    {
        public readonly ManualResetEventSlim Disconnected = new ManualResetEventSlim();
        public bool ThrowOnDisconnect;
        public override void OnConnected(EndPoint endPoint) { }
        public override void OnSend(int numOfBytes) { }
        public override void OnRecvPacket(ArraySegment<byte> buffer) => throw new InvalidOperationException("Expected regression-test handler failure");
        public override void OnDisconnected(EndPoint endPoint)
        {
            Disconnected.Set();
            if (ThrowOnDisconnect)
                throw new InvalidOperationException("Expected regression-test disconnect failure");
        }
    }

    private sealed class SocketPair : IDisposable
    {
        public readonly Socket Client;
        private readonly Session _session;

        public SocketPair(Session session)
        {
            _session = session;
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            Client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            Client.ReceiveTimeout = 10000;
            Client.SendTimeout = 10000;
            Client.Connect(listener.LocalEndpoint);
            Socket accepted = listener.AcceptSocket();
            listener.Stop();
            session.Start(accepted);
        }

        public void SendPacket(ushort id, byte[] body)
        {
            byte[] packet = new byte[body.Length + 4];
            Array.Copy(BitConverter.GetBytes((ushort)packet.Length), 0, packet, 0, 2);
            Array.Copy(BitConverter.GetBytes(id), 0, packet, 2, 2);
            Array.Copy(body, 0, packet, 4, body.Length);
            int sent = 0;
            while (sent < packet.Length)
                sent += Client.Send(packet, sent, packet.Length - sent, SocketFlags.None);
        }

        public byte[] ReadPacket()
        {
            byte[] header = ReadExactly(2);
            int size = BitConverter.ToUInt16(header, 0);
            Assert(size >= 4, "invalid response size");
            byte[] packet = new byte[size];
            Array.Copy(header, packet, 2);
            Array.Copy(ReadExactly(size - 2), 0, packet, 2, size - 2);
            return packet;
        }

        private byte[] ReadExactly(int count)
        {
            byte[] buffer = new byte[count];
            int received = 0;
            while (received < count)
            {
                int read = Client.Receive(buffer, received, count - received, SocketFlags.None);
                Assert(read > 0, "connection closed before the response");
                received += read;
            }
            return buffer;
        }

        public void Dispose()
        {
            _session.Disconnect();
            Client.Dispose();
        }
    }
}
