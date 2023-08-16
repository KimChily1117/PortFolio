using System;
using System.Net;
using System.Net.Sockets;

namespace ServerCore
{
    public class Connector
    {
        public Func<Session> _sessionFactory;

        public void Initialize(IPEndPoint endPoint , Func<Session> sessionFactory)
        {
            Socket socket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            _sessionFactory = sessionFactory;

            SocketAsyncEventArgs args = new SocketAsyncEventArgs();

            args.Completed += OnConnectCompleted;
            args.RemoteEndPoint = endPoint;
            args.UserToken = socket;
            
            RegisterAccept(args);

        }

        private void RegisterAccept(SocketAsyncEventArgs eventArgs)
        {
            Socket connectSocket = eventArgs.UserToken as Socket;

            bool isPending = connectSocket.ConnectAsync(eventArgs);

            if (isPending == false)
            {
                OnConnectCompleted(null,eventArgs);
            }
        }
        
        void OnConnectCompleted(object sender, SocketAsyncEventArgs args)
        {
            // 여기 부터 모르겠소요
            if (args.SocketError == SocketError.Success)
            {
                Session session = _sessionFactory.Invoke();
                session.Start(args.ConnectSocket);
                session.OnConected(args.RemoteEndPoint);
            }
            
        }
    }
}