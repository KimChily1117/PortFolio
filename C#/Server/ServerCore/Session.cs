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


        private bool isPending = false;
        
        private Queue<byte[]> _sendBuffer = new Queue<byte[]>(); 
        
        
        
        private SocketAsyncEventArgs _sendArgs = new SocketAsyncEventArgs();
        private SocketAsyncEventArgs _recvArgs = new SocketAsyncEventArgs();

        public void Start(Socket socket)
        {
            _socket = socket;

           

            byte[] recvBuff = new byte[1024];

            _recvArgs.SetBuffer(recvBuff, 0, 1024);
            _recvArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnRecvCompleted);
            
            
            _sendArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnSendComplete);


            RegisterRecv(_recvArgs);
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


        public void RegisterSend()
        {
            
            isPending = _socket.SendAsync(_sendArgs);
            if (isPending == false)
            {
                OnSendComplete(null, _sendArgs);
            }

            else
            {
               
            }
        }


        public void OnSendComplete(object sender, SocketAsyncEventArgs args)
        {

            if (args.SocketError == SocketError.Success && args.BytesTransferred > 0)

            {
                try
                {
                    if (isPending)
                    {
                        isPending = false;
                    }
                }
                catch (Exception ex)
                {

                }
            }

            else
            {
                Console.WriteLine($"OnSendComplete] Error!!!");
            }

        }

        #endregion Network(Send)


        public void Send(byte[] recvBuff)
        {

            _sendBuffer.Enqueue(recvBuff);
            
            RegisterSend();

        }

        public void Disconnect()
        {
            // 예외처리 추가 Socket통신은 별도의 스레드를 사용하여 연결 하기 때문에. Interlocked를 사용하여 동시에 발생하는 이벤트를
            // 방지 할 수 있다 
            if (Interlocked.Exchange(ref isDisconnect, 1) == 1)
            {
                return;
            }

            _socket.Shutdown(SocketShutdown.Both);
            _socket.Close();
        }

    }
}
