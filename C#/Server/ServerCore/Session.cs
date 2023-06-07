using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace ServerCore
{
    class Session
    {
        Socket _socket;



        public void Start(Socket socket)
        {
            _socket = socket;

            SocketAsyncEventArgs args = new SocketAsyncEventArgs();

            byte[] recvBuff = new byte[1024];

            args.SetBuffer(recvBuff, 0, 1024);

            args.Completed += OnRecvCompleted;

            RegisterRecv(args);
        }

        #region Network(Recv)
        public void RegisterRecv(SocketAsyncEventArgs eventArgs)
        {
            eventArgs.AcceptSocket = null;

            bool isPending = _socket.ReceiveAsync(eventArgs);

            if (isPending == false)
            {
                OnRecvCompleted(null, eventArgs);
            }
        }


        public void OnRecvCompleted(object sender, SocketAsyncEventArgs evtArgs)
        {
            if (evtArgs.BytesTransferred > 0 && evtArgs.SocketError == SocketError.Success)
            {
                try
                {
                    string recvData = Encoding.UTF8.GetString(evtArgs.Buffer,
                        evtArgs.Offset,
                        evtArgs.BytesTransferred);
                    Console.WriteLine($"Client -> Server : {recvData}");
                }
                catch (Exception)
                {
 
                    throw;
                }

            }
            else
            {

            }
        }
        #endregion Network(Recv)

        public void Send(byte[] buffer)
        {
            _socket.Send(buffer);
        }

        public void Disconnect()
        {
            _socket.Shutdown(SocketShutdown.Both);
            _socket.Close();
        }

    }
}
