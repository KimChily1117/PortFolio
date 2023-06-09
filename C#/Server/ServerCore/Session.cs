using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ServerCore
{
    class Session
    {
        Socket _socket;
        volatile int isDisconnect = 0;


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
                Disconnect();
            }
        }
        #endregion Network(Recv)


        #region Network(Send)


        public void RegisterSend(SocketAsyncEventArgs args)
        {
            bool isPending = _socket.SendAsync(args);

            if(isPending == false)
            {
                OnSendComplete(null , args);
            }

            else
            {
                return;
            }


        }


        public void OnSendComplete(object sender , SocketAsyncEventArgs args)
        {
            try
            {

            }
            catch (Exception ex)
            {

            }


        }

        #endregion Network(Send)


        public void Send(byte[] buffer)
        {

            SocketAsyncEventArgs args = new SocketAsyncEventArgs();

            byte[] recvBuff = new byte[1024];

            args.SetBuffer(recvBuff, 0, 1024);
            args.Completed += OnSendComplete;

            RegisterSend(args);

        }

        public void Disconnect()
        {
            // 예외처리 추가 Socket통신은 별도의 스레드를 사용하여 연결 하기 때문에. Interlocked를 사용하여 동시에 발생하는 이벤트를
            // 방지 할 수 있다 
            if(Interlocked.Exchange(ref isDisconnect ,1) == 1)
            {
                return; 
            }

            

            _socket.Shutdown(SocketShutdown.Both);
            _socket.Close();
        }

    }
}
